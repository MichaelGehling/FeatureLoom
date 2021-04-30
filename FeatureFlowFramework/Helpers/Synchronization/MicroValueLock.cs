using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public struct MicroValueLock
    {
        private const int NO_LOCK = 0;
        private const int WRITE_LOCK = NO_LOCK - 1;
        private const int FIRST_READ_LOCK = NO_LOCK + 1;

        const int DEFAULT_HOT_CYCLES = 0;
        const int DEFAULT_CYCLES_BEFORE_LOWERING_PRIO = DEFAULT_HOT_CYCLES + 2;

        int lockIndicator;
        ushort currentLockCount;
        bool readerBlocked;
        bool prioritizedWaiting;

        public bool IsLocked => lockIndicator == WRITE_LOCK;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnter(out LockHandle lockHandle, bool prioritized = false)
        {
            if (TryEnter(prioritized))
            {
                lockHandle = new LockHandle(false, currentLockCount, true);
                return true;
            }
            else
            {
                lockHandle = new LockHandle(false, currentLockCount, false);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryEnter(bool prioritized = false)
        {
            if (lockIndicator == NO_LOCK && (prioritized || !prioritizedWaiting) && Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK) == NO_LOCK) return true;
            else return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter(out LockHandle lockHandle, bool prioritized = false, int cyclesBeforeYielding = DEFAULT_HOT_CYCLES)
        {
            if (!TryEnter(prioritized)) Enter_Wait(prioritized, cyclesBeforeYielding); 

            lockHandle = new LockHandle(false, currentLockCount, true);
        }

        private void Enter_Wait(bool prioritized, int cyclesBeforeYielding)
        {
            bool loweredPrio = false;
            Thread currentThread = null;
            ThreadPriority origPriority = ThreadPriority.Normal;

            uint blockedByReaderCounter = 0;
            uint cycleCounter = 0;
            do
            {
                blockedByReaderCounter = HandleReaderBlocking(blockedByReaderCounter);

                if (prioritized) prioritizedWaiting = true;

                UpdateThreadPriority(ref loweredPrio, ref currentThread, ref origPriority, cycleCounter);

                if (cycleCounter >= cyclesBeforeYielding) Thread.Sleep(0);
                else cycleCounter++;

            } while (!TryEnter(prioritized));
            
            if (loweredPrio) currentThread.Priority = origPriority;
            if (prioritized) prioritizedWaiting = false;
            readerBlocked = false;
        }

        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryEnterReadOnly(bool prioritized = false)
        {
            if (readerBlocked && !prioritized) return false;

            int currentLockIndicator = lockIndicator;
            int newLockIndicator = currentLockIndicator + 1;
            return newLockIndicator >= FIRST_READ_LOCK && (prioritized || !prioritizedWaiting) && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnterReadOnly(out LockHandle lockHandle, bool prioritized = false)
        {
            lockHandle = new LockHandle(true, currentLockCount, false);
            if (readerBlocked && !prioritized) return false;

            int currentLockIndicator = lockIndicator;
            int newLockIndicator = currentLockIndicator + 1;
            if (newLockIndicator >= FIRST_READ_LOCK && (prioritized || !prioritizedWaiting) && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                lockHandle = new LockHandle(true, currentLockCount, true);
                return true;
            }
            else return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterReadOnly(out LockHandle lockHandle, bool prioritized = false, int cyclesBeforeYielding = DEFAULT_HOT_CYCLES)
        {
            if (!TryEnterReadOnly(prioritized)) EnterReadOnly_Wait(prioritized, cyclesBeforeYielding);
            lockHandle = new LockHandle(true, currentLockCount, true);
        }

        private void EnterReadOnly_Wait(bool prioritized, int cyclesBeforeYielding)
        {
            bool loweredPrio = false;
            Thread currentThread = null;
            ThreadPriority origPriority = ThreadPriority.Normal;

            uint cycleCounter = 0;
            do
            {
                if (prioritized) prioritizedWaiting = true;

                UpdateThreadPriority(ref loweredPrio, ref currentThread, ref origPriority, cycleCounter);

                if (cycleCounter >= cyclesBeforeYielding) Thread.Sleep(0);
                else cycleCounter++;

            } while (!TryEnterReadOnly(prioritized));

            if (loweredPrio) currentThread.Priority = origPriority;
            if (prioritized) prioritizedWaiting = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint HandleReaderBlocking(uint blockedByReaderCounter)
        {
            if (lockIndicator >= FIRST_READ_LOCK) blockedByReaderCounter++;
            else blockedByReaderCounter = 0;
            readerBlocked = blockedByReaderCounter > 1;

            return blockedByReaderCounter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateThreadPriority(ref bool loweredPrio, ref Thread currentThread, ref ThreadPriority origPriority, uint cycleCounter)
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit(LockHandle lockHandle)
        {
            if (lockIndicator == NO_LOCK) throw new MicroValueLockException("Exiting not acquired lock!");
            if (!lockHandle.acquired) throw new MicroValueLockException("Exiting with invalid lock handle: lock was not acquired!");
            if (lockHandle.validLockCount != currentLockCount) throw new MicroValueLockException("Exiting with invalid lock handle: lock coutn does not match!");
            if (lockHandle.readOnly && lockIndicator < FIRST_READ_LOCK) throw new MicroValueLockException("Exiting write lock with read lock handle!");
            if (!lockHandle.readOnly && lockIndicator != WRITE_LOCK) throw new MicroValueLockException("Exiting read lock with write lock handle!");

            if (lockHandle.readOnly)
            {
                if (lockIndicator == FIRST_READ_LOCK) currentLockCount++;
                Interlocked.Decrement(ref lockIndicator);
            }
            else
            {
                currentLockCount++;
                lockIndicator = NO_LOCK;
            }

        }

        public readonly struct LockHandle
        {
            internal readonly bool readOnly;
            internal readonly ushort validLockCount;
            internal readonly bool acquired;

            public LockHandle(bool readOnly, ushort validLockCount, bool acquired)
            {
                this.readOnly = readOnly;
                this.validLockCount = validLockCount;
                this.acquired = acquired;
            }
        }

        public class MicroValueLockException : Exception
        {
            public MicroValueLockException(string message) : base(message)
            {
            }

        }
    }
}
