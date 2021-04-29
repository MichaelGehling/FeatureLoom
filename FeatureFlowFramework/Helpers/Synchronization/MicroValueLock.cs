using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public struct MicroValueLock
    {
        const int NO_LOCK = 0;
        const int LOCKED = 1;

        const int DEFAULT_CYCLES_BEFORE_YIELDING = 0;
        const int DEFAULT_CYCLES_BEFORE_LOWERING_PRIO = DEFAULT_CYCLES_BEFORE_YIELDING + 2;

        int lockIndicator;

        public bool IsLocked => lockIndicator == LOCKED;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter(int cyclesBeforeYielding = DEFAULT_CYCLES_BEFORE_YIELDING)
        {
            if (Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) != NO_LOCK) Enter_Wait(cyclesBeforeYielding);            
        }

        private void Enter_Wait(int cyclesBeforeYielding)
        {
            bool loweredPrio = false;
            Thread currentThread = null;
            ThreadPriority origPriority = ThreadPriority.Normal;

            int cycleCounter = 0;
            do
            {
                if (!loweredPrio && cycleCounter >= DEFAULT_CYCLES_BEFORE_LOWERING_PRIO && currentThread == null)
                {
                    currentThread = Thread.CurrentThread;
                    origPriority = currentThread.Priority;
                    if (origPriority > ThreadPriority.Lowest)
                    {
                        loweredPrio = true;
                        currentThread.Priority = origPriority - 1;
                    }
                }

                if (cycleCounter >= cyclesBeforeYielding) Thread.Sleep(0);
                else cycleCounter++;
            } while (lockIndicator == LOCKED || Interlocked.CompareExchange(ref lockIndicator, LOCKED, NO_LOCK) != NO_LOCK);

            if (loweredPrio) currentThread.Priority = origPriority;
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
