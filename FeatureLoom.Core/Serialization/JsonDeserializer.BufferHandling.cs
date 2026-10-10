using FeatureLoom.Synchronization;
using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    public string ShowBufferAroundCurrentPosition(int before = 100, int after = 50) => buffer.ShowBufferAroundCurrentPosition(before, after);

    public void SkipBufferUntil(string delimiter, bool alsoSkipDelimiter, out bool found)
    {
        found = false;
        if (delimiter.EmptyOrNull()) return;

        serializerLock.Enter();
        try
        {
            SkipBufferUntilUnlocked(Encoding.UTF8.GetBytes(delimiter), alsoSkipDelimiter, out found);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    private static readonly ByteSegment lineFeedDelimiter = new byte[] { (byte)'\n' };

    /// <summary>
    /// JSON Lines recovery after a failed deserialization (caller must hold the lock).
    /// Rewinds to the start of the failed value before searching the line feed: a parser that
    /// already read into the next line must not cause that line to be skipped, too.
    /// Leading whitespace is skipped first, because it usually is the previous line's terminator.
    /// </summary>
    private void SkipFailedJsonLineUnlocked()
    {
        buffer.RewindToValueStart();
        SkipWhiteSpaces();
        SkipBufferUntilUnlocked(lineFeedDelimiter, true, out bool found);
        if (!found)
        {
            // Broken last line without terminator: the source is exhausted, so discard the rest
            // (the generic skip keeps trailing bytes in case of a delimiter split across reads).
            buffer.ResetBuffer(false, false);
        }
    }

    private void SkipBufferUntilUnlocked(ByteSegment delimiterBytes, bool alsoSkipDelimiter, out bool found)
    {
        found = false;
        try
        {
            if (buffer.CountRemainingBytes < delimiterBytes.Count)
            {
                if (buffer.CountSizeLeft == 0) buffer.ResetBuffer(true, false);
                buffer.TryReadFromStream();
            }
            do
            {
                if (!buffer.TryEnsureBuffered(1)) break; // ensure GetRemainingBytes has data
                ByteSegment bufferBytes = buffer.GetRemainingBytes();
                if (bufferBytes.TryFindIndex(delimiterBytes, out int index))
                {
                    found = true;
                    int bytesToSkip = index + (alsoSkipDelimiter ? delimiterBytes.Count : 0);
                    if (buffer.CountRemainingBytes == bytesToSkip)
                    {
                        //If the delimiter ends exactly at the end of the buffer, the last char will remain in the buffer

                        buffer.TrySkipBytes(1);
                        bytesToSkip--;
                        buffer.ResetBufferAfterFullSkip();
                        buffer.TryReadFromStream();
                    }
                    buffer.TrySkipBytes(bytesToSkip);
                    buffer.ResetAfterReading();
                    return;
                }
                buffer.TrySkipBytes(bufferBytes.Count - delimiterBytes.Count); //Ensure to keep the last chars for the case that the delimiter was split
                buffer.ResetBufferAfterFullSkip();
            }
            while (buffer.TryReadFromStream());
        }
        catch (Exception ex)
        {
            OptLog.ERROR()?.Build("Error occurred on skipping buffer.", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UndoReadHandle CreateUndoReadHandle(bool initUndo = true) => new UndoReadHandle(this, initUndo);

    /// <summary>
    /// Checks whether any non-whitespace data is left in the buffer or the stream.
    /// Blocks while waiting for stream data.
    /// </summary>
    /// <param name="ensureFullValue">
    /// If true, additionally reads until the complete next value (or JSON Lines record) is buffered
    /// or the stream has ended. Reaching the stream end still returns true, because data is left:
    /// either a valid value that can only be recognized as complete by the stream end (e.g. a trailing
    /// root number), or a truncated value, which the following <c>TryDeserialize</c> call reports as failure.
    /// Mainly for symmetry with <see cref="IsAnyDataLeftAsync(bool)"/>; the benefit is lower here,
    /// because the synchronous parser reads on demand anyway.
    /// </param>
    /// <returns>True if non-whitespace data is left, regardless of whether it forms a valid value.</returns>
    public bool IsAnyDataLeft(bool ensureFullValue = false)
    {
        serializerLock.Enter();
        try
        {
            if (!IsAnyDataLeftUnlocked()) return false;
            if (ensureFullValue)
            {
                while (!IsFullValueBufferedUnlocked())
                {
                    buffer.EnsureFreeSpaceForLine();
                    // Stream ended: data is still left (trailing root primitive or truncated value),
                    // so return true and let the parser either read it or report the error.
                    if (!buffer.TryReadFromStream()) break;
                }
            }
            return true;
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    static readonly Task<bool> trueResultTask = Task.FromResult(true);

    /// <summary>
    /// Asynchronously checks whether any non-whitespace data is left in the buffer or the stream.
    /// Waits for stream data without blocking a thread.
    /// </summary>
    /// <param name="ensureFullValue">
    /// If true, additionally waits until the complete next value (or JSON Lines record) is buffered
    /// or the stream has ended, so a following synchronous <c>TryDeserialize</c> call does not block on stream I/O.
    /// Reaching the stream end still returns true, because data is left: either a valid value that can only be
    /// recognized as complete by the stream end (e.g. a trailing root number), or a truncated value,
    /// which the following <c>TryDeserialize</c> call reports as failure.
    /// Recommended pattern: <c>while (await IsAnyDataLeftAsync(true)) TryDeserialize(out item);</c>.
    /// </param>
    /// <returns>True if non-whitespace data is left, regardless of whether it forms a valid value.</returns>
    public Task<bool> IsAnyDataLeftAsync(bool ensureFullValue = false)
    {
        // Fast path: value start (and optionally the full value) is already buffered, so complete synchronously.
        // Otherwise the held lock is handed over to the slow path, so it is only acquired once.
        serializerLock.Enter();
        try
        {
            if (ensureFullValue ? IsFullValueBufferedUnlocked() : buffer.TrySkipBufferedWhiteSpaces())
            {
                serializerLock.Exit();
                return trueResultTask;
            }
        }
        catch
        {
            serializerLock.Exit();
            throw;
        }
        return IsAnyDataLeftSlowAsync(ensureFullValue);
    }

    /// <summary>
    /// Checks without any I/O whether the complete next value is buffered.
    /// Skips buffered whitespace first, so the scan starts at the actual value start.
    /// Must only be called between root values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsFullValueBufferedUnlocked()
    {
        if (!buffer.TrySkipBufferedWhiteSpaces()) return false;
        if (settings.jsonLines) return buffer.IsLineFeedBuffered();
        buffer.BeginValueScan();
        return buffer.IsValueComplete();
    }

    // Expects the serializer lock to be held by the caller (see IsAnyDataLeftAsync) and releases it.
    private async Task<bool> IsAnyDataLeftSlowAsync(bool ensureFullValue)
    {
        // The lock must be held until the await completes, so this needs its own async method.
        try
        {
            buffer.ReadAheadEnabled = true;
            return await WaitForValueStartUnlockedAsync(ensureFullValue).ConfiguredAwait();
        }
        finally
        {
            buffer.ReadAheadEnabled = false;
            buffer.ScanLineFeeds = false;
            serializerLock.Exit();
        }
    }

    /// <summary>
    /// Asynchronously waits until the start of the next value is buffered, so that parsing does not
    /// block at the beginning of a value (e.g. waiting for the next JSON Lines record).
    /// For JSON Lines it additionally waits until the terminating line feed is buffered, so the
    /// complete record is available and the parser never blocks (the buffer grows up front if needed).
    /// Starts a background read-ahead afterwards. Returns false if the source has no further data.
    /// </summary>
    private async Task<bool> WaitForValueStartUnlockedAsync(bool ensureFullValue)
    {
        while (!buffer.TrySkipBufferedWhiteSpaces())
        {
            if (!await buffer.TryReadFromStreamAsync().ConfiguredAwait()) return false;
        }

        if (!ensureFullValue) { }
        else if (settings.jsonLines)
        {
            buffer.ScanLineFeeds = true;
            while (!buffer.IsLineFeedBuffered())
            {
                buffer.EnsureFreeSpaceForLine();
                // End of stream: the last record has no terminator, the parser handles the rest.
                if (!await buffer.TryReadFromStreamAsync().ConfiguredAwait()) break;
            }
        }
        else
        {
            // Same idea for any other input: a structural scan detects the end of the root value.
            buffer.BeginValueScan();
            while (!buffer.IsValueComplete())
            {
                buffer.EnsureFreeSpaceForLine();
                if (!await buffer.TryReadFromStreamAsync().ConfiguredAwait()) break;
            }
        }

        // Compact now (after the pending read is awaited) instead of blocking in the parser.
        if (buffer.NeedsCompaction)
        {
            if (buffer.HasPendingRead) await buffer.TryReadFromStreamAsync().ConfiguredAwait();
            buffer.ResetBuffer(true, false);
        }

        buffer.StartReadAhead();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAnyDataLeftUnlocked()
    {
        // Normal buffered state: current byte is still valid unread data.
        // EOF rollback state must be excluded (BufferReadTillEnd == true).
        if (buffer.CountRemainingBytes > 0 && !buffer.BufferReadTillEnd)
        {
            byte b = SkipWhiteSpaces();
            if (!IsWhiteSpace(b)) return true;
        }

        if (buffer.IsBufferCompletelyFilled) buffer.ResetBuffer(true, false);
        if (!buffer.TryReadFromStream()) return false;

        byte next = SkipWhiteSpaces();
        return !IsWhiteSpace(next);
    }



    public void SetDataSource(Stream stream)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(stream);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public void SetDataSource(string json)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(json);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public void SetDataSource(ByteSegment uft8Bytes)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(uft8Bytes);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public void SetDataSource(byte[] utf8Bytes, int offset, int count) => SetDataSource(new ByteSegment(utf8Bytes, offset, count));
    public void SetDataSource(byte[] utf8Bytes) => SetDataSource(new ByteSegment(utf8Bytes));

    public void SetDataSource(JsonFragment json, int offset, int count)
    {
        serializerLock.Enter();
        try
        {
            if (json.IsString) SetDataSourceUnlocked(json.JsonString);
            else if (json.IsUtf8) SetDataSourceUnlocked(json.JsonUtf8);
            else buffer.ResetBuffer(false, false);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    private void SetDataSourceUnlocked(Stream stream) => buffer.SetSource(stream);
    private void SetDataSourceUnlocked(string json) => buffer.SetSource(json);
    private void SetDataSourceUnlocked(ByteSegment jsonBytes) => buffer.SetSource(jsonBytes);
}
