using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Collections
{
    public sealed class CountingRingBuffer<T>
    {
        private T[] buffer;
        private int nextIndex = 0;
        private long counter = 0;
        private bool cycled = false;
        private LazyValue<AsyncManualResetEvent> newEntryEvent;
        private MicroValueLock myLock;
        private bool threadSafe = true;

        public CountingRingBuffer(int bufferSize, bool threadSafe = true)
        {
            buffer = new T[bufferSize];
            this.threadSafe = true;
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
                return newEntryEvent.Obj;
            }
        }

        public long Add(T item)
        {
            long result;
            if (threadSafe) myLock.Enter(true);
            try
            {
                buffer[nextIndex++] = item;
                if (nextIndex >= buffer.Length)
                {
                    nextIndex = 0;
                    cycled = true;
                }
                result = counter++;
            }
            finally
            {
                if (threadSafe) myLock.Exit();
            }

            newEntryEvent.ObjIfExists?.PulseAll();
            return result;
        }
        public long AddRange<IEnum>(IEnum items) where IEnum : IEnumerable<T>
        {
            if (threadSafe) myLock.Enter(true);
            try
            {
                foreach (var item in items)
                {
                    buffer[nextIndex++] = item;
                    if (nextIndex >= buffer.Length)
                    {
                        nextIndex = 0;
                        cycled = true;
                    }
                    counter++;
                }
            }
            finally
            {
                if (threadSafe) myLock.Exit();
            }

            newEntryEvent.ObjIfExists?.PulseAll();
            return counter;
        }

        public void Clear()
        {
            if (threadSafe) myLock.Enter(true);
            try
            {
                cycled = false;
                counter = 0;
                nextIndex = 0;
            }
            finally
            {
                if (threadSafe) myLock.Exit();
            }
        }

        public bool Contains(T item)
        {
            if (threadSafe) myLock.EnterReadOnly(true);
            try
            {
                int until = cycled ? buffer.Length : nextIndex;
                for (int i = 0; i < until; i++)
                {
                    if (buffer[i].Equals(item)) return true;
                }
                return false;
            }
            finally
            {
                if (threadSafe) myLock.ExitReadOnly();
            }
        }

        public T GetLatest()
        {
            if (threadSafe) myLock.EnterReadOnly(true);
            try
            {
                if (nextIndex == 0)
                {
                    if (cycled) return buffer[buffer.Length - 1];
                    else throw new ArgumentOutOfRangeException();
                }
                else return buffer[nextIndex - 1];
            }
            finally
            {
                if (threadSafe) myLock.ExitReadOnly();
            }
        }

        public bool TryGetFromNumber(long number, out T result)
        {
            if (threadSafe) myLock.EnterReadOnly(true);
            try
            {
                result = default;
                if (number >= counter || counter - number > Length) return false;

                int offset = (int)(counter - number);
                if (nextIndex - offset >= 0) result = buffer[nextIndex - offset];
                else result = buffer[buffer.Length - offset];
                return true;
            }
            finally
            {
                if (threadSafe) myLock.ExitReadOnly();
            }
        }

        public T[] GetAvailableSince(long startNumber, out long missed)
        {
            if (threadSafe) myLock.EnterReadOnly(true);
            try
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
            finally
            {
                if (threadSafe) myLock.ExitReadOnly();
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (threadSafe) myLock.EnterReadOnly(true);
            try
            {
                var leftSpace = array.Length - arrayIndex;
                CopyToInternal(array, arrayIndex, leftSpace > Length ? Length : leftSpace);
            }
            finally
            {
                if (threadSafe) myLock.ExitReadOnly();
            }
        }

        public void CopyTo(T[] array, int arrayIndex, int copyLength)
        {
            if (threadSafe) myLock.EnterReadOnly(true);
            try
            {
                CopyToInternal(array, arrayIndex, copyLength);
            }
            finally
            {
                if (threadSafe) myLock.ExitReadOnly();
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