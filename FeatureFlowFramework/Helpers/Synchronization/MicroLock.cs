using FeatureLoom.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace FeatureLoom.Helpers.Synchronization
{
    public sealed class MicroLock
    {
        MicroValueLock valueLock;

        public bool IsLocked => valueLock.IsLocked;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock(bool prioritized = false, int cyclesBeforeYielding = 0)
        {
            valueLock.Enter(prioritized, cyclesBeforeYielding);
            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if(valueLock.TryEnter(prioritized)) 
            {
                acquiredLock = new AcquiredLock(this, false);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock acquiredLock, TimeSpan timeout, bool prioritized = false, int numHotCycles = 0)
        {
            if (valueLock.TryEnter(timeout, prioritized, numHotCycles))
            {
                acquiredLock = new AcquiredLock(this, false);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly(bool prioritized = false, int cyclesBeforeYielding = 0)
        {
            valueLock.EnterReadOnly(prioritized, cyclesBeforeYielding);
            return new AcquiredLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (valueLock.TryEnterReadOnly(prioritized))
            {
                acquiredLock = new AcquiredLock(this, true);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out AcquiredLock acquiredLock, TimeSpan timeout, bool prioritized = false, int numHotCycles = 0)
        {
            if (valueLock.TryEnterReadOnly(timeout, prioritized, numHotCycles))
            {
                acquiredLock = new AcquiredLock(this, true);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        public struct AcquiredLock : IDisposable
        {
            MicroLock parentLock;
            readonly bool readOnly;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal AcquiredLock(MicroLock parentLock, bool readOnly)
            {
                this.parentLock = parentLock;
                this.readOnly = readOnly;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Exit();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {                
                if (readOnly) parentLock.valueLock.ExitReadOnly();
                else parentLock.valueLock.Exit();
                parentLock = null;
            }

            public bool IsActive => parentLock != null;
        }
    }
}
