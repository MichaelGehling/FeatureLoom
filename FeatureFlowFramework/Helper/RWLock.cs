using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace FeatureFlowFramework.Helper
{
    public class RWLock
    {
        const int NO_LOCKID = 0;
        const int WRITE_LOCKID = NO_LOCKID + 1;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockId larger than NO_LOCKID (0) implies a write-lock, while a lockId smaller than NO_LOCKID implies a read-lock.
        /// When entering a read-lock, the lockId is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockId (greater than NO_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockId = NO_LOCKID;
        volatile bool writerIsWaiting = false;
        volatile bool readerIsWaiting = false;

        ManualResetEventSlim mre = new ManualResetEventSlim(true, 0);
        
        SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.Balanced;

        public RWLock(SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.Balanced)
        {
            this.defaultSpinningBehaviour = defaultSpinningBehaviour;
        }

        public enum SpinWaitBehaviour
        {
            Balanced,
            NoSpinning,
            NoWaiting
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading()
        {
            return ForReading(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading(SpinWaitBehaviour spinWaitBehaviour = SpinWaitBehaviour.Balanced)
        {
            SpinWait spinWait = new SpinWait();
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;
            while(currentLockId > NO_LOCKID || (currentLockId < NO_LOCKID && writerIsWaiting) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                readerIsWaiting = true;
                if(spinWaitBehaviour == SpinWaitBehaviour.NoWaiting ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if(mre.IsSet) mre.Reset();
                    if(lockId > NO_LOCKID || (lockId < NO_LOCKID && writerIsWaiting))
                    {
                        mre.Wait();
                        spinWait.Reset();
                    }
                }

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            readerIsWaiting = false;

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockId = Interlocked.Increment(ref lockId);
            if(NO_LOCKID == newLockId)
            {
                if (!mre.IsSet) mre.Set();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting()
        {
            return ForWriting(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting(SpinWaitBehaviour spinWaitBehaviour = SpinWaitBehaviour.Balanced)
        {
            SpinWait spinWait = new SpinWait();
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while(currentLockId != NO_LOCKID || (currentLockId > NO_LOCKID && readerIsWaiting) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {
                writerIsWaiting = true;
                if(spinWaitBehaviour == SpinWaitBehaviour.NoWaiting ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if(mre.IsSet) mre.Reset();
                    if(lockId != NO_LOCKID || (lockId > NO_LOCKID && readerIsWaiting))
                    {
                        mre.Wait();
                        spinWait.Reset();
                    }
                }
                currentLockId = lockId;
            }
            writerIsWaiting = false;

            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockId = NO_LOCKID;
            if (!mre.IsSet) mre.Set();
        }

        public struct ReadLock : IDisposable
        {
            RWLock lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadLock(RWLock safeLock)
            {
                this.lockObj = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                lockObj.ExitReadLock();
            }
        }

        public struct WriteLock : IDisposable
        {
            RWLock lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WriteLock(RWLock safeLock)
            {
                this.lockObj = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                lockObj.ExitWriteLock();
            }
        }
    }
}
