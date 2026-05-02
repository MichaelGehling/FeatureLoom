using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Synchronization
{
    public sealed class MicroLock
    {
        private MicroValueLock valueLock;

        public bool IsLocked => valueLock.IsLocked;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle Lock(bool prioritized = false)
        {
            valueLock.Enter(prioritized);
            return new LockHandle(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out LockHandle lockHandle, bool prioritized = false)
        {
            if (valueLock.TryEnter(prioritized))
            {
                lockHandle = new LockHandle(this, false);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out LockHandle lockHandle, TimeSpan timeout, bool prioritized = false)
        {
            if (valueLock.TryEnter(timeout, prioritized))
            {
                lockHandle = new LockHandle(this, false);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle LockReadOnly(bool prioritized = false)
        {
            valueLock.EnterReadOnly(prioritized);
            return new LockHandle(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out LockHandle lockHandle, bool prioritized = false)
        {
            if (valueLock.TryEnterReadOnly(prioritized))
            {
                lockHandle = new LockHandle(this, true);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out LockHandle lockHandle, TimeSpan timeout, bool prioritized = false)
        {
            if (valueLock.TryEnterReadOnly(timeout, prioritized))
            {
                lockHandle = new LockHandle(this, true);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        public struct LockHandle : IDisposable
        {
            private MicroLock parentLock;
            private readonly bool readOnly;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal LockHandle(MicroLock parentLock, bool readOnly)
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