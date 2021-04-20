using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public struct MicroValueLock
    {
        const int NO_LOCK = 0;
        const int LOCKED = 1;

        const int DEFAULT_CYCLES_BEFORE_YIELDING = 200;

        int lockIndicator;

        public bool IsLocked => lockIndicator == LOCKED;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter(int cyclesBeforeYielding = DEFAULT_CYCLES_BEFORE_YIELDING)
        {
            if (Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) != NO_LOCK) Enter_Wait(cyclesBeforeYielding);            
        }

        private void Enter_Wait(int cyclesBeforeYielding)
        {
            int cycleCounter = 0;
            do
            {
                if (cycleCounter >= cyclesBeforeYielding) Thread.Sleep(0);
                else cycleCounter++;
            } while (lockIndicator == LOCKED || Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) != NO_LOCK);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnter()
        {
            if (lockIndicator == NO_LOCK && Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) == NO_LOCK) return true;
            else return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit()
        {
            lockIndicator = NO_LOCK;
        }
    }
}
