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

    public bool IsAnyDataLeft()
    {
        serializerLock.Enter();
        try
        {
            return IsAnyDataLeftUnlocked();
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    /// <summary>
    /// Asynchronously checks whether any non-whitespace data is left in the buffer or the stream.
    /// Waits for stream data without blocking a thread.
    /// </summary>
    public Task<bool> IsAnyDataLeftAsync()
    {
        // Fast path: a value start is already buffered, so complete synchronously without async state machine.
        serializerLock.Enter();
        try
        {
            if (buffer.TrySkipBufferedWhiteSpaces()) return Task.FromResult(true);
        }
        finally
        {
            serializerLock.Exit();
        }
        return IsAnyDataLeftSlowAsync();
    }

    private async Task<bool> IsAnyDataLeftSlowAsync()
    {
        // The lock must be held until the await completes, so this needs its own async method.
        serializerLock.Enter();
        try
        {
            buffer.ReadAheadEnabled = true;
            return await WaitForValueStartUnlockedAsync().ConfiguredAwait();
        }
        finally
        {
            buffer.ReadAheadEnabled = false;
            serializerLock.Exit();
        }
    }

    /// <summary>
    /// Asynchronously waits until the start of the next value is buffered, so that parsing does not
    /// block at the beginning of a value (e.g. waiting for the next JSON Lines record).
    /// Starts a background read-ahead afterwards. Returns false if the source has no further data.
    /// </summary>
    private async Task<bool> WaitForValueStartUnlockedAsync()
    {
        while (!buffer.TrySkipBufferedWhiteSpaces())
        {
            if (!await buffer.TryReadFromStreamAsync().ConfiguredAwait()) return false;
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
