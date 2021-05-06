using FeatureLoom.Synchronization;
using System;

namespace FeatureLoom.Collections
{
    public class CountingRingBuffer<T>
    {
        private T[] buffer;
        private int nextIndex = 0;
        private long counter = 0;
        private bool cycled = false;
        private AsyncManualResetEvent newEntryEvent;
        private FeatureLock myLock = new FeatureLock();

        public CountingRingBuffer(int bufferSize)
        {
            buffer = new T[bufferSize];
        }

        public int Length => cycled ? buffer.Length : nextIndex;
        public int MaxLength => buffer.Length;
        public long Counter => counter;
        public long Newest => counter - 1;
        public long Oldest => Newest - Length;

        public IAsyncWaitHandle WaitHandle
        {
            get
            {
                if (newEntryEvent == null) newEntryEvent = new AsyncManualResetEvent(false);
                return newEntryEvent.AsyncWaitHandle;
            }
        }

        public long Add(T item)
        {
            long result;
            using (myLock.Lock())
            {
                buffer[nextIndex++] = item;
                if (nextIndex >= buffer.Length)
                {
                    nextIndex = 0;
                    cycled = true;
                }
                result = counter++;
            }
            newEntryEvent?.Set();
            newEntryEvent?.Reset(); // TODO: Setting and directly resetting might fail waking up waiting threads!
            return result;
        }

        public void Clear()
        {
            using (myLock.Lock())
            {
                cycled = false;
                counter = 0;
                nextIndex = 0;
            }
        }

        public bool Contains(T item)
        {
            using (myLock.LockReadOnly())
            {
                int until = cycled ? buffer.Length : nextIndex;
                for (int i = 0; i < until; i++)
                {
                    if (buffer[i].Equals(item)) return true;
                }
                return false;
            }
        }

        public T GetLatest()
        {
            using (myLock.LockReadOnly())
            {
                if (nextIndex == 0)
                {
                    if (cycled) return buffer[buffer.Length - 1];
                    else throw new ArgumentOutOfRangeException();
                }
                else return buffer[nextIndex - 1];
            }
        }

        public bool TryGetFromNumber(long number, out T result)
        {
            using (myLock.LockReadOnly())
            {
                result = default;
                if (number >= counter || counter - number > Length) return false;

                int offset = (int)(counter - number);
                if (nextIndex - offset >= 0) result = buffer[nextIndex - offset];
                else result = buffer[buffer.Length - offset];
                return true;
            }
        }

        public T[] GetAvailableSince(long startNumber, out long missed)
        {
            using (myLock.LockReadOnly())
            {
                missed = 0;
                if (startNumber >= counter) return Array.Empty<T>();
                long numberToCopyLong = counter - startNumber;
                if (numberToCopyLong > Length)
                {
                    missed = numberToCopyLong - Length;
                    numberToCopyLong = Length;
                }
                int numberToCopy = (int)numberToCopyLong;
                T[] result = new T[numberToCopy];
                CopyToInternal(result, 0, numberToCopy);
                return result;
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            using (myLock.LockReadOnly())
            {
                var leftSpace = array.Length - arrayIndex;
                CopyToInternal(array, arrayIndex, leftSpace > Length ? Length : leftSpace);
            }
        }

        public void CopyTo(T[] array, int arrayIndex, int copyLength)
        {
            using (myLock.LockReadOnly())
            {
                CopyToInternal(array, arrayIndex, copyLength);
            }
        }

        private void CopyToInternal(T[] array, int arrayIndex, int copyLength)
        {
            var leftSpace = array.Length - arrayIndex;
            if (leftSpace < copyLength || copyLength > Length) throw new ArgumentOutOfRangeException();

            int frontBufferSize = nextIndex;
            int backBufferSize = Length - nextIndex;
            int copyFromFrontBuffer = copyLength >= frontBufferSize ? frontBufferSize : copyLength;
            int frontBufferStartIndex = frontBufferSize - copyFromFrontBuffer;
            int copyFromBackbuffer = copyLength - copyFromFrontBuffer;
            int backBufferStartIndex = nextIndex + backBufferSize - copyFromBackbuffer;
            if (copyFromBackbuffer > 0) Array.Copy(buffer, backBufferStartIndex, array, arrayIndex, copyFromBackbuffer);
            Array.Copy(buffer, frontBufferStartIndex, array, arrayIndex + copyFromBackbuffer, copyFromFrontBuffer);
        }
    }
}