using System;
using System.Collections.Generic;
using System.IO;
using FeatureLoom.Extensions;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;
using System.Collections.Specialized;
using FeatureLoom.Collections;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{
    private sealed class Buffer
    {
        byte[] buffer;
        int bufferPos = 0;
        int bufferResetLevel;
        
        readonly Stack<int> peekStack = new Stack<int>();

        int bufferStartPos = 0;
        int bufferFillLevel = 0;
        long totalBytesRead = 0;
        Stream stream;
        long lastStreamPosition = -1;
        bool bufferReadTillEnd = false;

        public byte CurrentByte => buffer[bufferPos];
        public int BufferPos => bufferPos;
        public bool BufferReadTillEnd => bufferReadTillEnd;

        public void Init(int bufferSize)
        {
            buffer = new byte[bufferSize];
            bufferResetLevel = (int)(bufferSize * 0.8);
        }

        public void SetSource(Stream stream)
        {            
            if (stream == this.stream && (!stream.CanSeek || lastStreamPosition == stream.Position)) return;

            ResetBuffer(false, false);
            this.stream = stream;
            if (stream.CanSeek) lastStreamPosition = stream.Position;
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

            bytes.CopyToArray(buffer, size);
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
                totalBytesRead += bytesRead;
                bufferFillLevel += bytesRead;
                if (stream.CanSeek) lastStreamPosition = stream.Position;
                result = bytesRead > 0;
            }
            catch
            {
                result = false;
            }
            return result;
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
                bufferPos = bytesToKeep;
                bufferStartPos = 0;
                bufferFillLevel = bytesToKeep;
            }
            else
            {
                bufferPos = 0;
                bufferStartPos = 0;
                bufferFillLevel = 0;
            }
            buffer = newBuffer;
            bufferReadTillEnd = false;
        }

        public void ResetAfterReading()
        {
            if (!(this.stream?.CanRead ?? false))
            {
                this.stream = null;
            }

            if (bufferPos >= bufferFillLevel)
            {
                bufferPos = 0;
                bufferFillLevel = 0;
            }
            bufferStartPos = bufferPos;
            peekStack.Clear();
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
            peekStack.Clear();
        }

        public string ShowBufferAroundCurrentPosition(int before = 100, int after = 50)
        {
            int startPos = (bufferPos - before).ClampLow(0);
            int endPos = (bufferPos + after).ClampHigh(bufferFillLevel - 1);
            ByteSegment segment = new ByteSegment(buffer, startPos, endPos - startPos + 1);
            return segment.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public struct UndoReadHandle : IDisposable
        {
            readonly private Buffer buffer;
            readonly private int startBufferPos;
            private bool undoReading;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool SetUndoReading(bool undo) => this.undoReading = undo;

            public UndoReadHandle(Buffer buffer, bool initUndo) : this()
            {
                this.buffer = buffer;
                undoReading = initUndo;
                startBufferPos = buffer.bufferPos;
                buffer.peekStack.Push(buffer.bufferPos);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ByteSegment GetReadBytes() => new ByteSegment(buffer.buffer, startBufferPos, buffer.bufferPos - startBufferPos + (buffer.BufferReadTillEnd ? 1 : 0));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                if (undoReading)
                {
                    var preRestorePos = buffer.bufferPos;
                    buffer.bufferPos = buffer.peekStack.Pop();
                    if (preRestorePos > buffer.bufferPos)
                    {
                        buffer.bufferReadTillEnd = false;
                    }
                }
                else buffer.peekStack.Pop();
            }
        }
    }
}
