using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{

    public class ReaderWriterLockSlimSubjects
    {
        ReaderWriterLockSlim myLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        ReaderWriterLockSlim reentrantLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public void Init()
        {
            myLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            reentrantLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            myLock.EnterWriteLock();
            try
            {
                action();
            }
            finally
            {
                myLock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly(Action action)
        {
            myLock.EnterReadLock();
            try
            {
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
            if(myLock.TryEnterWriteLock(0))
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
            if(myLock.TryEnterReadLock(0))
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
            reentrantLock.EnterWriteLock();
            try
            {
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
            reentrantLock.EnterReadLock();
            try
            {
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
            if(reentrantLock.TryEnterWriteLock(0))
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
            if(reentrantLock.TryEnterReadLock(0))
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
            myLock.EnterWriteLock();
            try
            {
            }
            finally
            {
                myLock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly()
        {
            myLock.EnterReadLock();
            try
            {
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
            reentrantLock.EnterWriteLock();
            try
            {
            }
            finally
            {
                reentrantLock.ExitWriteLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly()
        {
            reentrantLock.EnterReadLock();
            try
            {
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
