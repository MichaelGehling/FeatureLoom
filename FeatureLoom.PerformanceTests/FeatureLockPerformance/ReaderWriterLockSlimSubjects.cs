using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance
{
    public class ReaderWriterLockSlimSubjects
    {
        private ReaderWriterLockSlim myLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private ReaderWriterLockSlim reentrantLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public void Init()
        {
            myLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            reentrantLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            try
            {
                myLock.EnterWriteLock();
                action();
            }
            finally
            {
                if (myLock.IsWriteLockHeld) myLock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly(Action action)
        {
            try
            {
                myLock.EnterReadLock();
                action();
            }
            finally
            {
                myLock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(Action action)
        {
            if (myLock.TryEnterWriteLock(0))
            {
                try
                {
                    action();
                }
                finally
                {
                    myLock.ExitWriteLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly(Action action)
        {
            if (myLock.TryEnterReadLock(0))
            {
                try
                {
                    action();
                }
                finally
                {
                    myLock.ExitReadLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock(Action action)
        {
            try
            {
                reentrantLock.EnterWriteLock();
                action();
            }
            finally
            {
                reentrantLock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly(Action action)
        {
            try
            {
                reentrantLock.EnterReadLock();
                action();
            }
            finally
            {
                reentrantLock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock(Action action)
        {
            if (reentrantLock.TryEnterWriteLock(0))
            {
                try
                {
                    action();
                }
                finally
                {
                    reentrantLock.ExitWriteLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly(Action action)
        {
            if (reentrantLock.TryEnterReadLock(0))
            {
                try
                {
                    action();
                }
                finally
                {
                    reentrantLock.ExitReadLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            try
            {
                myLock.EnterWriteLock();
            }
            finally
            {
                myLock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly()
        {
            try
            {
                myLock.EnterReadLock();
            }
            finally
            {
                myLock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (myLock.TryEnterWriteLock(0))
            {
                try
                {
                }
                finally
                {
                    myLock.ExitWriteLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly()
        {
            if (myLock.TryEnterReadLock(0))
            {
                try
                {
                }
                finally
                {
                    myLock.ExitReadLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock()
        {
            try
            {
                reentrantLock.EnterWriteLock();
            }
            finally
            {
                reentrantLock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly()
        {
            try
            {
                reentrantLock.EnterReadLock();
            }
            finally
            {
                reentrantLock.ExitReadLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock()
        {
            if (reentrantLock.TryEnterWriteLock(0))
            {
                try
                {
                }
                finally
                {
                    reentrantLock.ExitWriteLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly()
        {
            if (reentrantLock.TryEnterReadLock(0))
            {
                try
                {
                }
                finally
                {
                    reentrantLock.ExitReadLock();
                }
            }
        }
    }
}