using System;
using System.Collections.Generic;
using System.IO;
using FeatureLoom.Extensions;
using System.Runtime.CompilerServices;
using FeatureLoom.Helpers;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonDeserializer
    {
        private sealed class Buffer
        {
            byte[] buffer;
            int bufferPos = 0;
            int bufferResetLevel;
            Stack<int> peekStack = new Stack<int>();
            int bufferStartPos = 0;
            int bufferFillLevel = 0;
            long totalBytesRead = 0;
            Stream stream;

            public byte CurrentByte => buffer[bufferPos];

            public void Init(int bufferSize)
            {
                buffer = new byte[bufferSize];
                bufferResetLevel = (int)(bufferSize * 0.8);
            }

            public void SetStream(Stream stream)
            {
                if (stream == this.stream) return;

                ResetBuffer(false, false);
                this.stream = stream;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryNextByte()
            {
                if (++bufferPos < bufferFillLevel) return true;
                if (TryReadFromStream()) return true;
                bufferPos--;
                return false;
            }

            public bool TryReadFromStream()
            {
                if (stream == null) return false;
                if (!stream.CanRead) return false;

                int bufferSizeLeft = buffer.Length - bufferFillLevel;
                if (bufferSizeLeft == 0) throw new BufferExceededException(); 
                try
                {
                    int bytesRead = stream.Read(buffer, bufferFillLevel, bufferSizeLeft);
                    totalBytesRead += bytesRead;
                    bufferFillLevel += bytesRead;                    
                    return bytesRead > 0;
                }
                catch (Exception e)
                {
                    return false;
                }
            }

            public bool TryPrepareDeserialization()
            {
                if (bufferStartPos > bufferResetLevel)
                {
                    ResetBuffer(true, false);
                    bufferPos = bufferStartPos;                    
                }
                else if (bufferPos >= bufferFillLevel - 1)
                {                    
                    if (!TryReadFromStream())
                    {
                        return false;
                    }                    
                }

                return true;
            }

            public void ResetBuffer(bool keepUnusedBytes, bool grow)
            {
                byte[] newBuffer = buffer;
                if (grow)
                {
                    newBuffer = new byte[buffer.Length * 2];
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
            }

            public void ResetAfterReading()
            {
                if (!(this.stream?.CanRead ?? false))
                {
                    this.stream = null;
                }

                if (bufferPos >= bufferFillLevel - 1)
                {
                    bufferPos = 0;
                    bufferFillLevel = 0;
                }
                bufferStartPos = bufferPos;
                peekStack.Clear();
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

            public bool IsBufferCompletelyFilled => bufferFillLevel == buffer.Length;
            public bool IsBufferReadToEnd => bufferPos >= bufferFillLevel;

            public Recording StartRecording(bool skipCurrent = false) => new Recording(this, skipCurrent);            
            internal struct Recording
            {
                int startBufferPos;
                Buffer buffer;

                public Recording(Buffer buffer, bool skipCurrent)
                {
                    this.buffer = buffer;
                    this.startBufferPos = buffer.bufferPos;
                    if (skipCurrent) this.startBufferPos++;
                }

                public ByteSegment GetRecordedBytes(bool includeCurrentByte)
                {
                    return new ByteSegment(buffer.buffer, startBufferPos, buffer.bufferPos - startBufferPos);
                }
            }

            public struct UndoReadHandle : IDisposable
            {
                private Buffer buffer;
                private int startBufferPos;
                private bool undoReading;

                public bool SetUndoReading(bool undo) => this.undoReading = undo;

                public UndoReadHandle(Buffer buffer, bool initUndo) : this()
                {
                    this.buffer = buffer;
                    undoReading = initUndo;
                    startBufferPos = buffer.bufferPos;
                    buffer.peekStack.Push(buffer.bufferPos);
                }

                public ByteSegment GetReadBytes() => new ByteSegment(buffer.buffer, startBufferPos, buffer.bufferPos - startBufferPos);

                public void Dispose()
                {
                    if (undoReading)
                    {
                        buffer.bufferPos = buffer.peekStack.Pop();
                    }
                    else buffer.peekStack.Pop();
                }
            }
        }
    }
}
