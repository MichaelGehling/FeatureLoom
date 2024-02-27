using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FeatureLoom.Helpers
{
    public class SlicedBuffer<T> where T : struct
    {
        T[] buffer;
        List<Slice> freeSlices = new List<Slice>();
        int parentId = 0;

        FeatureLock myLock = new FeatureLock();

        public SlicedBuffer(int bufferSize) 
        {            
            buffer = new T[bufferSize];
            Reset();
         
        }

        public void Reset()
        {
            using (myLock.Lock())
            {
                parentId++;
                freeSlices.Clear();
                freeSlices.Add(new Slice(this, 0, buffer.Length));
            }
        }

        public bool TryGetSlice(int size, out Slice slice) 
        {
            using (myLock.Lock())
            {
                int lastIndex = freeSlices.Count - 1;
                for (int i = 0; i < freeSlices.Count; i++)
                {
                    var freeSlice = freeSlices[i];
                    if (freeSlice.count < size) continue;
                    if (freeSlice.count > size)
                    {
                        slice = new Slice(this, freeSlice.firstIndex, size);
                        var remainingSlice = new Slice(this, freeSlice.firstIndex + size, freeSlice.count - size);
                        freeSlices[i] = remainingSlice;
                        return true;
                    }
                    if (freeSlice.count == size)
                    {
                        slice = freeSlice;
                        if (i < lastIndex) freeSlices[i] = freeSlices[lastIndex];
                        freeSlices.RemoveAt(lastIndex);
                        return true;
                    }
                }

                slice = default;
                return false;
            }
        }

        void ReturnSlice(Slice slice)
        {
            using (myLock.Lock())
            {
                if (slice.parentId != parentId) return;            
                const int NOT_FOUND = -1;
                const int NOT_EXIST = -2;

                int leftBufferIndex = slice.firstIndex;
                int rightBufferIndex = slice.firstIndex + slice.count;
                int leftNeighborIndex = NOT_FOUND;
                int rightNeighborIndex = NOT_FOUND;
                if (leftBufferIndex == 0) leftNeighborIndex = NOT_EXIST;
                if (rightBufferIndex == buffer.Length) rightNeighborIndex = NOT_EXIST;
                for (int i = 0; i < freeSlices.Count; i++)
                {
                    var freeSlice = freeSlices[i];
                    if (leftNeighborIndex == NOT_FOUND)
                    {
                        if (freeSlice.firstIndex + freeSlice.count == leftBufferIndex)
                        {
                            leftNeighborIndex = i;
                            continue;
                        }
                    }
                    if (rightNeighborIndex == NOT_FOUND)
                    {
                        if (freeSlice.firstIndex == rightBufferIndex)
                        {
                            rightNeighborIndex = i;
                            continue;
                        }
                    }
                    if (rightNeighborIndex != NOT_FOUND && leftNeighborIndex != NOT_FOUND) break;
                }


                if (leftNeighborIndex > NOT_FOUND)
                {
                    var leftNeighbor = freeSlices[leftNeighborIndex];
                    slice.firstIndex = leftNeighbor.firstIndex;
                    slice.count += leftNeighbor.count;
                }
                if (rightNeighborIndex > NOT_FOUND)
                {
                    var rightNeighbor = freeSlices[rightNeighborIndex];
                    slice.count += rightNeighbor.count;
                }
                if (leftNeighborIndex > NOT_FOUND)
                {
                    int lastIndex = freeSlices.Count - 1;
                    freeSlices[leftNeighborIndex] = freeSlices[lastIndex];
                    freeSlices.RemoveAt(lastIndex);

                    if (lastIndex == rightNeighborIndex) rightNeighborIndex = leftNeighborIndex;
                }
                if (rightNeighborIndex > NOT_FOUND)
                {
                    int lastIndex = freeSlices.Count - 1;
                    freeSlices[rightNeighborIndex] = freeSlices[lastIndex];
                    freeSlices.RemoveAt(lastIndex);
                }

                freeSlices.Add(slice);
            }
        }

        public struct Slice : IDisposable
        {
            internal SlicedBuffer<T> parent;
            internal int firstIndex;
            internal int count;
            internal int parentId;

            internal Slice(SlicedBuffer<T> parent, int firstIndex, int count)
            {
                this.parent = parent;
                this.firstIndex = firstIndex;
                this.count = count;
                this.parentId = parent.parentId;
            }

            public bool IsValid => parent?.parentId == parentId;
            public int Count => count;
            public T this[int key]
            {
                get => parent.buffer[firstIndex + key];
                set => parent.buffer[firstIndex + key] = value;
            }

            public void CopyTo(T[] targetBuffer, int targetOffset)
            {
                if (count > targetBuffer.Length + targetOffset) throw new OutOfMemoryException();

                Array.Copy(parent.buffer, firstIndex, targetBuffer, targetOffset, count);
            }
            public void CopyTo(Slice targetBuffer, int targetOffset)
            {
                if (count > targetBuffer.count + targetOffset) throw new OutOfMemoryException();

                Array.Copy(parent.buffer, firstIndex, targetBuffer.parent.buffer, targetBuffer.firstIndex + targetOffset, count);
            }

            public void CopyTo(T[] targetBuffer, int targetOffset, int count)
            {
                if (count > this.count) throw new ArgumentOutOfRangeException("count");
                if (count > targetBuffer.Length + targetOffset) throw new ArgumentOutOfRangeException("count");

                Array.Copy(parent.buffer, firstIndex, targetBuffer, targetOffset, count);
            }

            public void CopyTo(Slice targetBuffer, int targetOffset, int count)
            {
                if (count > this.count) throw new ArgumentOutOfRangeException("count");
                if (count > targetBuffer.count + targetOffset) throw new ArgumentOutOfRangeException("count");

                Array.Copy(parent.buffer, firstIndex, targetBuffer.parent.buffer, targetBuffer.firstIndex + targetOffset, count);
            }

            public void CopyTo(T[] targetBuffer, int targetOffset, int offset, int count)
            {
                if (offset + count > this.count) throw new ArgumentOutOfRangeException("offset+count");
                if (count > targetBuffer.Length + targetOffset) throw new ArgumentOutOfRangeException("count");

                Array.Copy(parent.buffer, firstIndex + offset, targetBuffer, targetOffset, count);
            }

            public void CopyTo(Slice targetBuffer, int targetOffset, int offset, int count)
            {
                if (offset + count > this.count) throw new ArgumentOutOfRangeException("offset+count");
                if (count > targetBuffer.count + targetOffset) throw new ArgumentOutOfRangeException("count");

                Array.Copy(parent.buffer, firstIndex + offset, targetBuffer.parent.buffer, targetBuffer.firstIndex + targetOffset, count);
            }

            public void CopyFrom(T[] sourceBuffer, int sourceOffset, int offset, int count)
            {
                if (offset + count > this.count) throw new ArgumentOutOfRangeException("offset+count");

                Array.Copy(sourceBuffer, sourceOffset, parent.buffer, firstIndex + offset, count);
            }

            public void CopyFrom(T[] sourceBuffer, int sourceOffset, int count)
            {
                if (count > this.count) throw new ArgumentOutOfRangeException("count");

                Array.Copy(sourceBuffer, sourceOffset, parent.buffer, firstIndex, count);
            }

            /// <summary>
            /// Trims the slice by keeping the first part and returning the remaining part.
            /// </summary>
            /// <param name="newSize">The resulting size of the slice. Must be smaller than the current size</param>
            /// <exception cref="ArgumentOutOfRangeException">Is thrown if newSize is greater than count</exception>
            public void Trim(int newSize)
            {
                Split(newSize).Dispose();
            }

            /// <summary>
            /// Splits the slice by keeping the first part with the new size and returns
            /// the second part with the remaining size.
            /// IMPORTANT: The returned slice must be assigned and disposed if no longer needed.
            ///            If the remaining part is no longer needed, use Trim()!
            /// </summary>
            /// <param name="newSize">The resulting size of the slice. Must be smaller than the current size</param>
            /// <returns>The remaining part</returns>
            /// <exception cref="ArgumentOutOfRangeException">Is thrown if newSize is greater than count</exception>
            public Slice Split(int newSize)
            {
                if (newSize > count) throw new ArgumentOutOfRangeException("count");
                Slice rest = new Slice(parent, firstIndex + newSize, count - newSize);
                this.count = newSize;
                return rest;
            }

            public ArraySegment<T> AsArraySegment() => new ArraySegment<T>(parent.buffer, firstIndex, count);

            public void Dispose()
            {
                parent?.ReturnSlice(this);
                parent = null;
            }
        }
    }
}
