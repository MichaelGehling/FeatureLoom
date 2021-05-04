using FeatureLoom.Helpers.Time;
using FeatureLoom.Services;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.Helpers.Synchronization
{
    public struct MicroValueLock
    {
        private const int NO_LOCK = 0;
        private const int WRITE_LOCK = NO_LOCK - 1;
        private const int FIRST_READ_LOCK = NO_LOCK + 1;

        const int DEFAULT_HOT_CYCLES = 0;

        int lockIndicator;
        bool readerBlocked;
        bool prioritizedWaiting;

        public bool IsLocked => lockIndicator == WRITE_LOCK;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnter(bool prioritized = false)
        {
            if (lockIndicator == NO_LOCK && (prioritized || !prioritizedWaiting) && Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK) == NO_LOCK) return true;
            else return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter(bool prioritized = false, int numHotCycles = DEFAULT_HOT_CYCLES)
        {
            if (!TryEnter(prioritized)) Enter_Wait(prioritized, numHotCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnter(TimeSpan timeout, bool prioritized = false, int numHotCycles = DEFAULT_HOT_CYCLES)
        {
            if (TryEnter(prioritized)) return true;
            else if (Enter_Wait_Timeout(timeout, prioritized, numHotCycles)) return true;
            else return false;
        }

        private void Enter_Wait(bool prioritized, int numHotCycles)
        {
            uint blockedByReaderCounter = 0;
            uint cycleCounter = 0;
            do
            {
                blockedByReaderCounter = HandleReaderBlocking(blockedByReaderCounter);

                if (prioritized) prioritizedWaiting = true;

                if (cycleCounter >= numHotCycles) Thread.Sleep(0);
                else cycleCounter++;

            } while (!TryEnter(prioritized));
            
            if (prioritized) prioritizedWaiting = false;
            readerBlocked = false;
        }

        private bool Enter_Wait_Timeout(TimeSpan timeout, bool prioritized, int numHotCycles)
        {
            TimeFrame timer = new TimeFrame(AppTime.Now, timeout);
            uint blockedByReaderCounter = 0;
            uint cycleCounter = 0;
            do
            {
                

                blockedByReaderCounter = HandleReaderBlocking(blockedByReaderCounter);

                if (prioritized) prioritizedWaiting = true;

                if (cycleCounter >= numHotCycles)
                {
                    if (timer.Elapsed(AppTime.CoarseNow + AppTime.CoarsePrecision) && timer.Elapsed(AppTime.Now))
                    {
                        if (prioritized) prioritizedWaiting = false;
                        return false;
                    }
                    else Thread.Sleep(0);
                }
                else cycleCounter++;

            } while (!TryEnter(prioritized));

            if (prioritized) prioritizedWaiting = false;
            readerBlocked = false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnterReadOnly(bool prioritized = false)
        {
            if (readerBlocked && !prioritized) return false;

            int currentLockIndicator = lockIndicator;
            int newLockIndicator = currentLockIndicator + 1;
            return newLockIndicator >= FIRST_READ_LOCK && (prioritized || !prioritizedWaiting) && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterReadOnly(bool prioritized = false, int numHotCycles = DEFAULT_HOT_CYCLES)
        {
            if (!TryEnterReadOnly(prioritized)) EnterReadOnly_Wait(prioritized, numHotCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnterReadOnly(TimeSpan timeout, bool prioritized = false, int numHotCycles = DEFAULT_HOT_CYCLES)
        {
            if (TryEnterReadOnly(prioritized)) return true;
            else if (EnterReadOnly_Wait_Timeout(timeout, prioritized, numHotCycles)) return true;
            else return false;
        }

        private void EnterReadOnly_Wait(bool prioritized, int numHotCycles)
        {
            uint cycleCounter = 0;
            do
            {
                if (prioritized) prioritizedWaiting = true;

                if (cycleCounter >= numHotCycles) Thread.Sleep(0);
                else cycleCounter++;

            } while (!TryEnterReadOnly(prioritized));

            if (prioritized) prioritizedWaiting = false;
        }

        private bool EnterReadOnly_Wait_Timeout(TimeSpan timeout, bool prioritized, int numHotCycles)
        {
            TimeFrame timer = new TimeFrame(AppTime.Now, timeout);
            uint cycleCounter = 0;
            do
            {
                if (prioritized) prioritizedWaiting = true;

                if (cycleCounter >= numHotCycles)
                {
                    if (timer.Elapsed(AppTime.CoarseNow + AppTime.CoarsePrecision) && timer.Elapsed(AppTime.Now))
                    {
                        if (prioritized) prioritizedWaiting = false;
                        return false;
                    }
                    else Thread.Sleep(0);
                }
                else cycleCounter++;

            } while (!TryEnterReadOnly(prioritized));

            if (prioritized) prioritizedWaiting = false;
            return true;
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
        public void Exit()
        {
            if (lockIndicator != WRITE_LOCK)
            {
                if (lockIndicator == NO_LOCK) throw new MicroValueLockException("Exiting not acquired lock!");
                else throw new MicroValueLockException("Trying to exit read lock state with Exit() instead of ExitReadOnly()!");
            }

            lockIndicator = NO_LOCK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitReadOnly()
        {
            if (lockIndicator < FIRST_READ_LOCK)
            {
                if (lockIndicator == NO_LOCK) throw new MicroValueLockException("Exiting not acquired lock!");
                else throw new MicroValueLockException("Trying to exit write lock state with ExitReadLock() instead of Exit()!");
            }

            Interlocked.Decrement(ref lockIndicator);
        }

        public class MicroValueLockException : Exception
        {
            public MicroValueLockException(string message) : base(message)
            {
            }

        }
    }
}
