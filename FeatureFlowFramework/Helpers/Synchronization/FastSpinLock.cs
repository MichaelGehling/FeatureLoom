using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class FastSpinLock
    {
        const int CYCLES_BEFORE_YIELDING = 200;
        const int NO_LOCK = 0;
        const int LOCKED = 1;

        int lockIndicator = NO_LOCK;

        public bool IsLocked => Thread.VolatileRead(ref lockIndicator) == LOCKED;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock()
        {
            int cycleCounter = 0;
            if(Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) != NO_LOCK)
            {
                do
                {
                    if(cycleCounter >= CYCLES_BEFORE_YIELDING) Thread.Yield();
                    else cycleCounter++;
                } while(Thread.VolatileRead(ref lockIndicator) == LOCKED || Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) != NO_LOCK);
            }
            return new AcquiredLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock acquiredLock)
        {
            if(Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) == NO_LOCK) 
            {
                acquiredLock = new AcquiredLock(this);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock acquiredLock, TimeSpan timeout)
        {
            if(Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) == NO_LOCK)
            {
                acquiredLock = new AcquiredLock(this);
                return true;
            }

            TimeFrame timer = new TimeFrame(timeout);
            int cycleCounter = 0;
            do
            {
                if(timer.Elapsed)
                {
                    acquiredLock = new AcquiredLock();
                    return false;
                }

                if(cycleCounter >= CYCLES_BEFORE_YIELDING) Thread.Yield();
                else cycleCounter++;

            } while(Thread.VolatileRead(ref lockIndicator) == LOCKED || Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) != NO_LOCK);

            acquiredLock = new AcquiredLock(this);
            return true;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Exit()
        {
            Thread.VolatileWrite(ref lockIndicator, NO_LOCK);
        }

        public struct AcquiredLock : IDisposable
        {
            FastSpinLock parentLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal AcquiredLock(FastSpinLock parentLock)
            {
                this.parentLock = parentLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Exit();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                parentLock.Exit();
                parentLock = null;
            }

            public bool IsActive => parentLock != null;
        }
    }
}
