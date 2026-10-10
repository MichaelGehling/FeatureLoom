using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FeatureLoom.Extensions;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;
using System.Collections.Specialized;
using FeatureLoom.Collections;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    private sealed class Buffer
    {
        byte[] buffer;
        int bufferPos = 0;
        int bufferResetLevel;

        int bufferStartPos = 0;
        int bufferFillLevel = 0;
        long totalBytesRead = 0;
        Stream stream;
        // Stream.CanSeek/CanRead are virtual calls whose result does not change while the same
        // source is used, so they are cached when the source is set.
        bool streamCanSeek = false;
        long lastStreamPosition = -1;
        bool bufferReadTillEnd = false;

        // Async read-ahead: at most one stream read is in flight. It writes into the free tail of the
        // buffer (beyond bufferFillLevel), which the parser never touches, so no synchronization is
        // needed on the hot path. Its bytes are only taken over (and bufferFillLevel updated) by the
        // parser thread. Anything that moves or discards buffer data must complete it first.
        Task<int> pendingRead;
        bool readAheadEnabled;
        public bool ReadAheadEnabled { get => readAheadEnabled; set => readAheadEnabled = value; }

        // JSON Lines line feed tracking (async only): a raw LF can only be a record terminator, so a
        // buffered LF behind the value start proves that the complete root value is buffered.
        // The read-ahead task scans its own bytes in parallel to parsing; the result is only taken
        // over by the parser thread after the task completed (no further synchronization needed).
        bool scanLineFeeds;
        bool pendingReadScanned;
        int pendingReadLastLf = -1;
        int lastLfPos = -1;   // position of a known LF in the buffer, or -1
        int lfScanPos = 0;    // bytes before this position were scanned (lastLfPos is the last LF found there)
        public bool ScanLineFeeds { get => scanLineFeeds; set => scanLineFeeds = value; }

        // Value end scan (async only, non JSON Lines): a cheap structural scan (brackets, strings, escapes)
        // that detects when the complete root value is buffered, so the synchronous parser can run
        // without ever waiting (blocking) for stream data. State is resumable across reads.
        int valueScanPos = -1;
        int valueScanStartPos = -1;
        int valueScanDepth;
        bool valueScanInString;
        bool valueScanEscape;
        bool valueScanStarted;
        bool valueScanComplete;
        public bool HasPendingRead => pendingRead != null;
        public bool NeedsCompaction => bufferStartPos > bufferResetLevel;

        public byte CurrentByte => buffer[bufferPos];
        public int BufferPos { get{ return bufferPos; } set{ bufferPos = value; } }
        public bool BufferReadTillEnd { get{ return bufferReadTillEnd; } set{ bufferReadTillEnd = value; } }

        public byte[] InternalBuffer => buffer;

        public void Init(int bufferSize)
        {
            buffer = new byte[bufferSize];
            bufferResetLevel = (int)(bufferSize * 0.8);
        }

        public void SetSource(Stream stream)
        {
            // A pending read advances the stream position, so it must be taken over before the
            // position is compared, otherwise it would be mistaken for an external seek.
            if (pendingRead != null) CompletePendingRead();
            // When the same stream is reused, the cached streamCanSeek is used instead of querying
            // the virtual Stream.CanSeek again. On a FileStream that property is backed by a
            // SafeFileHandle check and is surprisingly expensive to call on every deserialization.
            if (stream == this.stream && (!streamCanSeek || lastStreamPosition == stream.Position)) return;

            bool canSeek = stream.CanSeek;
            ResetBuffer(false, false);
            this.stream = stream;
            this.streamCanSeek = canSeek;
            lastStreamPosition = canSeek ? stream.Position : -1;
        }

        public void SetSource(string str)
        {
            this.stream = null;

            int expectedSize = (int)(str.Length * 1.2);
            if (expectedSize <= buffer.Length) ResetBuffer(false, false);
            else ResetBuffer(false, true, expectedSize);

            try
            {
                bufferFillLevel = Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);
            }
            catch
            {
                int maxRequiredSize = str.Length * 2;
                ResetBuffer(false, true, maxRequiredSize);
                bufferFillLevel = Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);
            }
        }

        public void SetSource(ByteSegment bytes)
        {
            this.stream = null;

            int size = bytes.Count;
            if (size < buffer.Length) ResetBuffer(false, false);
            else ResetBuffer(false, true, size);

            // Bulk copy: ByteSegment is backed by a contiguous ArraySegment, so the generic
            // IEnumerable-based CopyToArray (which copies byte by byte) must be avoided here.
            var source = bytes.AsArraySegment;
            Array.Copy(source.Array, source.Offset, buffer, 0, size);
            bufferFillLevel = size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryNextByte()
        {
            if (++bufferPos < bufferFillLevel) return true;
            return TryNextByte_Continuation();
        }

        private bool TryNextByte_Continuation()
        {
            if (TryReadFromStream()) return true;
            bufferReadTillEnd = true;
            bufferPos--;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySkipBytes(int count)
        {
            if (count <= 0) return true;

            int bytesLeft = bufferFillLevel - bufferPos;
            if (count > bytesLeft) return false;

            int target = bufferPos + count;
            bufferPos = (target < bufferFillLevel) ? target : (bufferFillLevel - 1);
            return true;
        }

        public bool TryReadFromStream()
        {
            if (pendingRead != null)
            {
                // Blocks if the read-ahead has not completed yet (accepted sync-over-async compromise,
                // because the parser is synchronous).
                if (!CompletePendingRead()) return false;
                StartReadAhead();
                return true;
            }

            if (stream == null) return false;
            if (!stream.CanRead) return false;

            int bufferSizeLeft = buffer.Length - bufferFillLevel;
            if (bufferSizeLeft == 0)
            {
                throw new BufferExceededException();
            }
            bool result;
            try
            {
                int bytesRead = stream.Read(buffer, bufferFillLevel, bufferSizeLeft);
                ApplyRead(bytesRead);
                result = bytesRead > 0;
            }
            catch
            {
                result = false;
            }
            if (result) StartReadAhead();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyRead(int bytesRead)
        {
            totalBytesRead += bytesRead;
            bufferFillLevel += bytesRead;
            // Reads are the only place that advances the stream, so the position can be tracked
            // incrementally instead of querying the virtual Stream.Position on every read.
            // SetSource() still detects an external seek, because any position change that did
            // not originate here will differ from the tracked value.
            if (streamCanSeek) lastStreamPosition += bytesRead;
        }

        /// <summary>Starts a background read into the free buffer tail, if enabled and possible.</summary>
        public void StartReadAhead()
        {
            if (!readAheadEnabled || pendingRead != null || stream == null) return;
            int free = buffer.Length - bufferFillLevel;
            if (free <= 0) return;
            try
            {
                pendingReadLastLf = -1;
                pendingReadScanned = scanLineFeeds;
                pendingRead = scanLineFeeds ? ReadAndScanAsync(stream, buffer, bufferFillLevel, free) : stream.ReadAsync(buffer, bufferFillLevel, free);
            }
            catch
            {
                pendingRead = null;
            }
        }

        private async Task<int> ReadAndScanAsync(Stream source, byte[] target, int offset, int count)
        {
            int bytesRead = await source.ReadAsync(target, offset, count).ConfiguredAwait();
            pendingReadLastLf = bytesRead > 0 ? FindLastLineFeed(target, offset, bytesRead) : -1;
            return bytesRead;
        }

        private static int FindLastLineFeed(byte[] data, int offset, int count)
        {
#if NETSTANDARD2_0
            return Array.LastIndexOf(data, (byte)'\n', offset + count - 1, count);
#else
            int index = new ReadOnlySpan<byte>(data, offset, count).LastIndexOf((byte)'\n');
            return index < 0 ? -1 : offset + index;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyPendingRead(int bytesRead)
        {
            int offset = bufferFillLevel;
            ApplyRead(bytesRead);
            if (!pendingReadScanned) return;
            if (pendingReadLastLf >= 0) lastLfPos = pendingReadLastLf;
            if (lfScanPos == offset) lfScanPos = bufferFillLevel;
        }

        /// <summary>
        /// Returns true if a line feed is buffered behind the current position. Only scans bytes
        /// that were not scanned before (e.g. by the read-ahead task).
        /// </summary>
        public bool IsLineFeedBuffered()
        {
            if (lastLfPos >= bufferPos) return true;
            int from = Math.Max(lfScanPos, bufferPos);
            int count = bufferFillLevel - from;
            if (count <= 0) return false;
            lfScanPos = bufferFillLevel;
            int index = FindLastLineFeed(buffer, from, count);
            if (index < 0) return false;
            lastLfPos = index;
            return true;
        }

        /// <summary>
        /// Starts a new value end scan at the current position (must be the value start).
        /// If a scan for the same value start exists already, it is kept, so its progress is reused.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginValueScan()
        {
            if (valueScanStartPos == bufferPos && valueScanPos >= 0) return;
            valueScanStartPos = bufferPos;
            valueScanPos = bufferPos;
            valueScanDepth = 0;
            valueScanInString = false;
            valueScanEscape = false;
            valueScanStarted = false;
            valueScanComplete = false;
        }

        /// <summary>
        /// Continues the value end scan over newly buffered bytes. Returns true if the end of the root
        /// value is buffered. Root primitives (numbers, literals) end at the first following delimiter,
        /// so at the very end of the stream the caller must stop waiting on its own.
        /// </summary>
        public bool IsValueComplete()
        {
            if (valueScanComplete) return valueScanPos < bufferFillLevel;
            int i = valueScanPos;
            int end = bufferFillLevel;
            byte[] data = buffer;
            while (i < end)
            {
#if !NETSTANDARD2_0
                // Vectorized jumps over the bytes that cannot change the scan state, like the parser's
                // string/whitespace skipping does. Only the byte at the found index is handled below.
                if (!valueScanEscape)
                {
                    var remaining = new ReadOnlySpan<byte>(data, i, end - i);
                    int idx;
                    if (valueScanInString) idx = remaining.IndexOfAny((byte)'"', (byte)'\\');
                    else if (valueScanDepth > 0) idx = IndexOfStructural(remaining);
                    else if (valueScanStarted) idx = IndexOfPrimitiveEnd(remaining);
                    else idx = 0;
                    if (idx < 0) { i = end; break; }
                    i += idx;
                }
#endif
                if (valueScanInString)
                {
                    byte s = data[i++];
                    if (valueScanEscape) valueScanEscape = false;
                    else if (s == (byte)'\\') valueScanEscape = true;
                    else if (s == (byte)'"')
                    {
                        valueScanInString = false;
                        if (valueScanDepth == 0) return CompleteValueScan(i);
                    }
                    continue;
                }

                byte b = data[i++];
                // A started root primitive (number, literal) ends at any delimiter; the delimiter only
                // needs to be buffered, it is not part of the value.
                if (valueScanStarted && valueScanDepth == 0 &&
                    (b == (byte)'"' || b == (byte)'{' || b == (byte)'[' || b == (byte)'}' || b == (byte)']'))
                {
                    return CompleteValueScan(i);
                }
                switch (b)
                {
                    case (byte)'"':
                        valueScanInString = true;
                        valueScanStarted = true;
                        break;
                    case (byte)'{':
                    case (byte)'[':
                        valueScanDepth++;
                        valueScanStarted = true;
                        break;
                    case (byte)'}':
                    case (byte)']':
                        if (--valueScanDepth <= 0) return CompleteValueScan(i);
                        break;
                    case (byte)' ':
                    case (byte)'\t':
                    case (byte)'\r':
                    case (byte)'\n':
                    case (byte)',':
                        if (valueScanStarted && valueScanDepth == 0) return CompleteValueScan(i);
                        break;
                    default:
                        valueScanStarted = true;
                        break;
                }
            }
            valueScanPos = i;
            return false;
        }

        #if NET8_0_OR_GREATER
        static readonly System.Buffers.SearchValues<byte> structuralSearchValues = System.Buffers.SearchValues.Create("\"{}[]"u8);
        static readonly System.Buffers.SearchValues<byte> primitiveEndSearchValues = System.Buffers.SearchValues.Create(" \t\r\n,{}[]\""u8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOfStructural(ReadOnlySpan<byte> span) => span.IndexOfAny(structuralSearchValues);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOfPrimitiveEnd(ReadOnlySpan<byte> span) => span.IndexOfAny(primitiveEndSearchValues);
#elif !NETSTANDARD2_0
        static readonly byte[] structuralBytes = { (byte)'"', (byte)'{', (byte)'}', (byte)'[', (byte)']' };
        static readonly byte[] primitiveEndBytes = { (byte)' ', (byte)'\t', (byte)'\r', (byte)'\n', (byte)',', (byte)'{', (byte)'}', (byte)'[', (byte)']', (byte)'"' };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOfStructural(ReadOnlySpan<byte> span) => span.IndexOfAny(structuralBytes);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOfPrimitiveEnd(ReadOnlySpan<byte> span) => span.IndexOfAny(primitiveEndBytes);
#endif

        private bool CompleteValueScan(int pos)
        {
            valueScanPos = pos;
            valueScanComplete = true;
            // The parser peeks the byte following the value (delimiter check), so that byte must be buffered too.
            // If it is not yet, the caller keeps waiting; stream end is handled by the caller.
            return pos < bufferFillLevel;
        }

        /// <summary>
        /// Ensures free space for the next read while waiting for a complete line: compacts if the
        /// current value does not start at the beginning, otherwise grows the buffer.
        /// Must only be called with the read position at the value start and no pending read.
        /// </summary>
        public void EnsureFreeSpaceForLine()
        {
            if (pendingRead != null || bufferFillLevel < buffer.Length) return;
            ResetBuffer(true, bufferStartPos == 0);
        }

        /// <summary>Waits (blocking) for the pending read and takes over its bytes. Returns false on end of stream or error.</summary>
        private bool CompletePendingRead()
        {
            var task = pendingRead;
            pendingRead = null;
            int bytesRead;
            try
            {
                bytesRead = task.GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
            ApplyPendingRead(bytesRead);
            return bytesRead > 0;
        }

        /// <summary>
        /// Skips whitespace in the already buffered data only (no I/O).
        /// Returns true if a non-whitespace byte is buffered at the current position.
        /// If everything is consumed and no read is pending, the buffer is cleared for reuse.
        /// Must only be called between root values.
        /// </summary>
        public bool TrySkipBufferedWhiteSpaces()
        {
            // EOF rollback state: the last byte was already consumed.
            if (bufferReadTillEnd)
            {
                bufferPos = bufferFillLevel;
                bufferReadTillEnd = false;
            }

            while (bufferPos < bufferFillLevel)
            {
                byte b = buffer[bufferPos];
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r') break;
                bufferPos++;
            }
            bufferStartPos = bufferPos;
            if (bufferPos < bufferFillLevel) return true;

            if (pendingRead == null)
            {
                bufferPos = 0;
                bufferStartPos = 0;
                bufferFillLevel = 0;
                ResetLineFeedTracking();
            }
            return false;
        }

        /// <summary>
        /// Asynchronously reads more data into the free buffer tail, taking over a pending read-ahead if present.
        /// Returns false on end of stream, error or if no stream source is set.
        /// </summary>
        public async Task<bool> TryReadFromStreamAsync()
        {
            if (pendingRead == null)
            {
                if (stream == null || !stream.CanRead) return false;
                bool wasEnabled = readAheadEnabled;
                readAheadEnabled = true;
                StartReadAhead();
                readAheadEnabled = wasEnabled;
                if (pendingRead == null) return false;
            }

            var task = pendingRead;
            int bytesRead;
            try
            {
                bytesRead = await task.ConfiguredAwait();
            }
            catch
            {
                pendingRead = null;
                return false;
            }
            pendingRead = null;
            ApplyPendingRead(bytesRead);
            return bytesRead > 0;
        }

        private int EffectiveRemainingCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (bufferFillLevel - bufferPos - (bufferReadTillEnd ? 1 : 0)).ClampLow(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPrepareDeserialization()
        {
            if (bufferReadTillEnd) return false;

            if (bufferStartPos > bufferResetLevel)
            {
                ResetBuffer(true, false);
                bufferPos = bufferStartPos;
            }
            else if (bufferPos >= bufferFillLevel)
            {
                if (!TryReadFromStream())
                {
                    return false;
                }                    
            }

            return true;
        }

        public void ResetBuffer(bool keepUnusedBytes, bool grow, int newSize = 0)
        {
            if (pendingRead != null) CompletePendingRead();
            byte[] newBuffer = buffer;
            if (grow)
            {
                if (newSize <= 0) newSize = buffer.Length * 2;
                newBuffer = new byte[newSize];
                bufferResetLevel = (int)(newBuffer.Length * 0.8);
            }

            if (keepUnusedBytes)
            {
                int bytesToKeep = bufferFillLevel - bufferStartPos;
                Array.Copy(buffer, bufferStartPos, newBuffer, 0, bytesToKeep);
                // Keep the read position relative to the moved data (callers like TryEnsureBuffered
                // compact in the middle of a token and must continue at the same byte).
                bufferPos = (bufferPos - bufferStartPos).Clamp(0, bytesToKeep);
                lastLfPos = lastLfPos >= bufferStartPos ? lastLfPos - bufferStartPos : -1;
                lfScanPos = (lfScanPos - bufferStartPos).Clamp(0, bytesToKeep);
                if (valueScanPos >= 0)
                {
                    if (valueScanStartPos >= bufferStartPos)
                    {
                        valueScanPos = (valueScanPos - bufferStartPos).Clamp(0, bytesToKeep);
                        valueScanStartPos -= bufferStartPos;
                    }
                    else valueScanPos = valueScanStartPos = -1;
                }
                bufferStartPos = 0;
                bufferFillLevel = bytesToKeep;
            }
            else
            {
                bufferPos = 0;
                bufferStartPos = 0;
                bufferFillLevel = 0;
                ResetLineFeedTracking();
            }
            buffer = newBuffer;
            bufferReadTillEnd = false;
        }

        public void ResetAfterReading()
        {
            if (this.stream != null && !this.stream.CanRead)
            {
                this.stream = null;
            }

            if (bufferPos >= bufferFillLevel && pendingRead == null)
            {
                bufferPos = 0;
                bufferFillLevel = 0;
                ResetLineFeedTracking();
            }
            bufferStartPos = bufferPos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetLineFeedTracking()
        {
            lastLfPos = -1;
            lfScanPos = 0;
            // Positions are reused for new data, so a previous value scan is no longer valid.
            valueScanPos = valueScanStartPos = -1;
        }

        public void ResetBufferAfterFullSkip()
        {
            bufferStartPos = bufferPos;
            ResetBuffer(true, false);
            bufferPos = bufferStartPos;
        }

        public void ResetAfterBufferExceededException()
        {
            bool growBuffer = bufferStartPos < (int)(buffer.Length * 0.5);
            ResetBuffer(true, growBuffer);
            bufferPos = bufferStartPos;
        }

        public string ShowBufferAroundCurrentPosition(int before = 100, int after = 50)
        {
            int startPos = (bufferPos - before).ClampLow(0);
            int endPos = (bufferPos + after).ClampHigh(bufferFillLevel - 1);
            ByteSegment segment = new ByteSegment(buffer, startPos, endPos - startPos + 1);
            return segment.ToString();
        }

        /// <summary>Moves the read position back to the start of the current (failed) value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RewindToValueStart()
        {
            bufferPos = bufferStartPos;
            bufferReadTillEnd = false;
        }

        public ByteSegment GetRemainingBytes() => new ByteSegment(buffer, bufferPos, bufferFillLevel - bufferPos);
#if !NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> GetRemainingSpan()
        {
            return new ReadOnlySpan<byte>(buffer, bufferPos, bufferFillLevel - bufferPos);
        }
#endif

        public int CountRemainingBytes => bufferFillLevel - bufferPos;
        public int CountSizeLeft => buffer.Length - bufferFillLevel;

#if NET5_0_OR_GREATER
        /// <summary>
        /// Resolves a complete string value that is already fully buffered and contains no
        /// escape sequence, advancing the position past the closing quote. The current byte
        /// must be the opening quote.
        /// <para>
        /// This is the common case by far, and handling it here allows the whole string to be
        /// located with a single vectorized scan and a single position update, instead of
        /// going through TryNextByte/TrySkipBytes round trips. Returns false without changing
        /// any state when the string is escaped or not fully buffered, so the caller can fall
        /// back to the general loop.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadSimpleStringBytes(out ByteSegment stringBytes)
        {
            int contentStart = bufferPos + 1;
            int available = bufferFillLevel - contentStart;
            if (available > 0)
            {
                var span = new ReadOnlySpan<byte>(buffer, contentStart, available);
                int specialIndex = span.IndexOfAny((byte)'"', (byte)'\\');
                if (specialIndex >= 0 && span[specialIndex] == (byte)'"')
                {
                    // The closing quote was found inside the buffered data, so the value is
                    // complete and escape-free. Leave the position on the byte after the quote,
                    // matching what the general loop's trailing TryNextByte would produce.
                    int closingQuotePos = contentStart + specialIndex;
                    if (closingQuotePos + 1 < bufferFillLevel)
                    {
                        stringBytes = new ByteSegment(buffer, contentStart, specialIndex);
                        bufferPos = closingQuotePos + 1;
                        return true;
                    }
                }
            }

            stringBytes = default;
            return false;
        }
#endif

        public bool IsBufferCompletelyFilled => bufferFillLevel == buffer.Length;
        public bool IsBufferReadToEnd => bufferFillLevel == 0 || bufferPos >= bufferFillLevel - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Recording StartRecording(bool skipCurrent = false) => new Recording(this, skipCurrent);            
        internal readonly struct Recording
        {
            readonly int startBufferPos;
            readonly Buffer buffer;

            public Recording(Buffer buffer, bool skipCurrent)
            {
                this.buffer = buffer;
                this.startBufferPos = buffer.bufferPos;
                if (skipCurrent) this.startBufferPos++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ByteSegment GetRecordedBytes(bool includeCurrentByte)
            {
                int count = buffer.bufferPos - startBufferPos;
                if (includeCurrentByte) count++;
                return new ByteSegment(buffer.buffer, startBufferPos, count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ByteSegment GetRecordedBytes_WithoutCurrent()
            {
                int count = buffer.bufferPos - startBufferPos;
                return new ByteSegment(buffer.buffer, startBufferPos, count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAvailableBufferedCount()
        {
            // excludes the EOF rollback phantom byte
            int count = bufferFillLevel - bufferPos - (bufferReadTillEnd ? 1 : 0);
            Debug.Assert(count >= 0);
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnsureBuffered(int minBytes)
        {
            while (GetAvailableBufferedCount() < minBytes)
            {
                if (bufferFillLevel == buffer.Length)
                {
                    // compact, keep unread bytes
                    ResetBuffer(true, false);
                }

                if (!TryReadFromStream()) return GetAvailableBufferedCount() >= minBytes;
            }

            return true;
        }
    }

    public class BufferExceededException : Exception
    {

    }

    public struct UndoReadHandle : IDisposable
    {
        readonly private Buffer buffer;
        readonly private int startBufferPos;
        private bool undoReading;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetUndoReading(bool undo) => this.undoReading = undo;

        internal UndoReadHandle(JsonDeserializer deserializer, bool initUndo) : this()
        {
            buffer = deserializer.buffer;
            undoReading = initUndo;
            startBufferPos = buffer.BufferPos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment GetReadBytes() => new ByteSegment(buffer.InternalBuffer, startBufferPos, buffer.BufferPos - startBufferPos + (buffer.BufferReadTillEnd ? 1 : 0));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (undoReading)
            {
                UndoNow();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UndoNow()
        {
            var preRestorePos = buffer.BufferPos;
            buffer.BufferPos = startBufferPos;
            if (preRestorePos > buffer.BufferPos)
            {
                buffer.BufferReadTillEnd = false;
            }            
        }
    }
}
