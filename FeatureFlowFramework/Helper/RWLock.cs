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
        SpinWait spinWait = new SpinWait();
        ManualResetEventSlim mre = new ManualResetEventSlim(true);
        SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.BalancedSpinning;

        public RWLock(SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.BalancedSpinning)
        {
            this.defaultSpinningBehaviour = defaultSpinningBehaviour;
        }

        public enum SpinWaitBehaviour
        {
            BalancedSpinning,
            NoSpinning,
            OnlySpinning
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading()
        {
            return ForReading(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading(SpinWaitBehaviour spinWaitBehaviour = SpinWaitBehaviour.BalancedSpinning)
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if(currentLockId > NO_LOCKID)
                {
                    if(spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                        (spinWaitBehaviour == SpinWaitBehaviour.BalancedSpinning && !spinWait.NextSpinWillYield))
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    if(mre.IsSet) mre.Reset();
                    if (lockId > NO_LOCKID) mre.Wait();
                }
                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockId = Interlocked.Increment(ref lockId);
            if(NO_LOCKID == newLockId)
            {
                spinWait.Reset();
                if (!(mre?.IsSet ?? true))
                {
                    mre.Set();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting()
        {
            return ForWriting(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting(SpinWaitBehaviour spinWaitBehaviour = SpinWaitBehaviour.BalancedSpinning)
        {
            var newLockId = WRITE_LOCKID;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if (currentLockId != NO_LOCKID)
                {
                    if(spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                        (spinWaitBehaviour == SpinWaitBehaviour.BalancedSpinning && !spinWait.NextSpinWillYield))
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    if(mre.IsSet) mre.Reset();
                    if(lockId != NO_LOCKID) mre.Wait();
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));

            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockId = NO_LOCKID;
            spinWait.Reset();
            if(!mre.IsSet) mre.Set();
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
