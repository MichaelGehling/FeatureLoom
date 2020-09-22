using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
{

    public class ReaderWriterLockSlimSubjects
    {
        ReaderWriterLockSlim myLock;
        ReaderWriterLockSlim reentrantLock;

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
    }
}
