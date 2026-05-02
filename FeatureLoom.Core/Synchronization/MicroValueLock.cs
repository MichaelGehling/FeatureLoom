using FeatureLoom.Time;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.Synchronization;

public struct MicroValueLock
{
    private const int NO_LOCK = 0;
    private const int WRITE_LOCK = NO_LOCK - 1;
    private const int FIRST_READ_LOCK = NO_LOCK + 1;

    // NOTE: The order of the variables matters for performance.
    private volatile bool readerBlocked;
    private volatile bool prioritizedWaiting;
    private volatile int lockIndicator;

    public bool IsLocked => lockIndicator != NO_LOCK;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnter(bool prioritized = false) 
    {
        return (prioritized || !prioritizedWaiting) && Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK) == NO_LOCK;            
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enter(bool prioritized = false)
    {
        if (TryEnter(prioritized)) return;
        Enter_Wait(prioritized);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnter(TimeSpan timeout, bool prioritized = false)
    {
        if (TryEnter(prioritized)) return true;
        if (Enter_Wait_Timeout(timeout, prioritized)) return true;
        return false;
    }

    private void Enter_Wait(bool prioritized)
    {
        uint blockedByReaderCounter = 0;
        do
        {
            blockedByReaderCounter = HandleReaderBlocking(blockedByReaderCounter);
            if (prioritized) prioritizedWaiting = true;
            Thread.Yield();
        } while (!TryEnter(prioritized));

        if (prioritized) prioritizedWaiting = false;
        readerBlocked = false;
    }

    private bool Enter_Wait_Timeout(TimeSpan timeout, bool prioritized)
    {
        TimeFrame timer = new TimeFrame(AppTime.Now, timeout);
        uint blockedByReaderCounter = 0;
        do
        {
            blockedByReaderCounter = HandleReaderBlocking(blockedByReaderCounter);

            if (prioritized) prioritizedWaiting = true;

            if (timer.Elapsed(AppTime.CoarseNow + AppTime.CoarsePrecision) && timer.Elapsed(AppTime.Now))
            {
                if (prioritized) prioritizedWaiting = false;
                return false;
            }
            else Thread.Yield();

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
    public void EnterReadOnly(bool prioritized = false)
    {
        if (TryEnterReadOnly(prioritized)) return;
        EnterReadOnly_Wait(prioritized);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnterReadOnly(TimeSpan timeout, bool prioritized = false)
    {
        if (TryEnterReadOnly(prioritized)) return true;
        if (EnterReadOnly_Wait_Timeout(timeout, prioritized)) return true;
        return false;
    }

    private void EnterReadOnly_Wait(bool prioritized)
    {
        do
        {
            if (prioritized) prioritizedWaiting = true;
            Thread.Yield();
        } while (!TryEnterReadOnly(prioritized));

        if (prioritized) prioritizedWaiting = false;
    }

    private bool EnterReadOnly_Wait_Timeout(TimeSpan timeout, bool prioritized)
    {
        TimeFrame timer = new TimeFrame(AppTime.Now, timeout);
        do
        {
            if (prioritized) prioritizedWaiting = true;

            if (timer.Elapsed(AppTime.CoarseNow + AppTime.CoarsePrecision) && timer.Elapsed(AppTime.Now))
            {
                if (prioritized) prioritizedWaiting = false;
                return false;
            }
            else Thread.Yield();
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

        public MicroValueLockException() : base()
        {
        }

        public MicroValueLockException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}