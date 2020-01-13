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

    public class RWSpinLock
    {
        const int NO_LOCKID = int.MaxValue-1;
        const int WRITE_LOCKID = NO_LOCKID + 1;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockId larger than NO_LOCKID (0) implies a write-lock, while a lockId smaller than NO_LOCKID implies a read-lock.
        /// When entering a read-lock, the lockId is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockId (greater than NO_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockId = NO_LOCKID;
        SpinWait spinWait = new SpinWait();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading()
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if (currentLockId > NO_LOCKID)
                {
                        spinWait.SpinOnce();
                        continue;
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
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting()
        {
            var newLockId = WRITE_LOCKID;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if(currentLockId != NO_LOCKID)
                {
                    spinWait.SpinOnce();
                    continue;
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
        }

        public struct ReadLock : IDisposable
        {
            RWSpinLock lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadLock(RWSpinLock safeLock)
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
            RWSpinLock lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WriteLock(RWSpinLock safeLock)
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
