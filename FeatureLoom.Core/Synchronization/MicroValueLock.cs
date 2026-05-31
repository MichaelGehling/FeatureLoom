using FeatureLoom.Time;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.Synchronization;

/// <summary>
/// A lightweight, struct-based reader-writer lock optimised for short critical sections with low contention.
/// In Release builds and on uncontested paths it reduces to a single <c>CMPXCHG</c> (acquire) and a single
/// <c>MOV</c> (release), making it significantly faster than <see cref="System.Threading.Monitor"/> when
/// there is no congestion.
/// </summary>
/// <remarks>
/// <para>
/// Waiting is implemented via <see cref="Thread.Yield"/>, which is self-tuning: on a lightly loaded
/// system the thread is rescheduled almost immediately (approaching spin-loop latency), while on a busy
/// system other threads naturally receive CPU time, allowing the lock holder to finish sooner.
/// For critical sections longer than a few microseconds a <see cref="System.Threading.Monitor"/>-based
/// lock will scale better because it suspends waiters rather than yielding.
/// </para>
/// <para>
/// <b>Copy warning:</b> Because this is a value type, copying it (e.g. <c>var copy = myLock</c>) creates
/// an independent instance that shares no state with the original. Always store <see cref="MicroValueLock"/>
/// as a non-readonly field of a class (or access it via <c>ref</c>). Declaring it <c>readonly</c> causes
/// the C# compiler to silently operate on defensive copies, completely defeating the locking semantics.
/// </para>
/// <para>
/// Misuse guards (calling <see cref="Exit"/> without holding the write lock, etc.) are active only in
/// DEBUG builds and compiled away in Release for maximum throughput.
/// </para>
/// </remarks>
public struct MicroValueLock
{
    private const int NO_LOCK = 0;
    private const int WRITE_LOCK = NO_LOCK - 1;
    private const int FIRST_READ_LOCK = NO_LOCK + 1;

    // NOTE: The order of the variables matters for performance.
    private volatile bool readerBlocked;
    private volatile bool prioritizedWaiting;
    private volatile int lockIndicator;

    /// <summary>
    /// Gets a value indicating whether the lock is currently held by any reader or writer.
    /// This is a non-atomic snapshot and should be used for diagnostics only.
    /// </summary>
    public bool IsLocked => lockIndicator != NO_LOCK;

    /// <summary>
    /// Attempts to acquire the write lock without blocking.
    /// </summary>
    /// <param name="prioritized">
    /// When <c>true</c> the attempt bypasses any queued prioritized waiter, allowing the caller
    /// to compete even when another thread has raised the priority flag.
    /// </param>
    /// <returns><c>true</c> if the write lock was acquired; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnter(bool prioritized = false) 
    {
        return (prioritized || !prioritizedWaiting) && Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK) == NO_LOCK;            
    }

    /// <summary>
    /// Acquires the write lock, yielding the thread until it becomes available.
    /// </summary>
    /// <param name="prioritized">
    /// When <c>true</c> the thread signals to readers that a writer is waiting, causing new
    /// non-prioritized readers to back off and preventing writer starvation.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enter(bool prioritized = false)
    {
        if (TryEnter(prioritized)) return;
        Enter_Wait(prioritized);
    }

    /// <summary>
    /// Attempts to acquire the write lock, yielding the thread until the lock becomes available
    /// or the specified timeout elapses.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the write lock.</param>
    /// <param name="prioritized">See <see cref="Enter(bool)"/>.</param>
    /// <returns><c>true</c> if the write lock was acquired within the timeout; otherwise <c>false</c>.</returns>
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
                readerBlocked = false;
                return false;
            }
            else Thread.Yield();

        } while (!TryEnter(prioritized));

        if (prioritized) prioritizedWaiting = false;
        readerBlocked = false;

        return true;
    }

    /// <summary>
    /// Attempts to acquire a read lock without blocking.
    /// Multiple readers may hold the read lock simultaneously; acquisition fails only when a
    /// writer holds the lock, a writer is waiting with <c>prioritized = true</c>, or reader
    /// blocking is active to prevent writer starvation.
    /// </summary>
    /// <param name="prioritized">When <c>true</c> the attempt bypasses the reader-blocked and
    /// prioritized-waiter checks, allowing high-priority readers to proceed regardless.</param>
    /// <returns><c>true</c> if a read lock was acquired; otherwise <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnterReadOnly(bool prioritized = false)
    {
        if (readerBlocked && !prioritized) return false;

        int currentLockIndicator = lockIndicator;
        int newLockIndicator = currentLockIndicator + 1;
        return newLockIndicator >= FIRST_READ_LOCK && (prioritized || !prioritizedWaiting) && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator);
    }

    /// <summary>
    /// Acquires a read lock, yielding the thread until one becomes available.
    /// </summary>
    /// <param name="prioritized">See <see cref="TryEnterReadOnly(bool)"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterReadOnly(bool prioritized = false)
    {
        if (TryEnterReadOnly(prioritized)) return;
        EnterReadOnly_Wait(prioritized);
    }

    /// <summary>
    /// Attempts to acquire a read lock, yielding the thread until one becomes available or the
    /// specified timeout elapses.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for a read lock.</param>
    /// <param name="prioritized">See <see cref="TryEnterReadOnly(bool)"/>.</param>
    /// <returns><c>true</c> if a read lock was acquired within the timeout; otherwise <c>false</c>.</returns>
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

    /// <summary>
    /// Releases the write lock acquired by <see cref="Enter(bool)"/> or
    /// <see cref="TryEnter(bool)"/> / <see cref="TryEnter(TimeSpan,bool)"/>.
    /// </summary>
    /// <remarks>
    /// In DEBUG builds this method throws <see cref="MicroValueLockException"/> if the write lock
    /// is not currently held. In Release builds the guard is elided for maximum throughput.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Exit()
    {
#if DEBUG
        if (lockIndicator != WRITE_LOCK)
        {
            if (lockIndicator == NO_LOCK) throw new MicroValueLockException("Exiting not acquired lock!");
            else throw new MicroValueLockException("Trying to exit read lock state with Exit() instead of ExitReadOnly()!");
        }
#endif
        // Release store: on ARM emits a barrier (stlr), on x86 a plain store suffices (hardware TSO).
        Volatile.Write(ref lockIndicator, NO_LOCK);
    }

    /// <summary>
    /// Releases one read lock acquired by <see cref="EnterReadOnly(bool)"/> or
    /// <see cref="TryEnterReadOnly(bool)"/> / <see cref="TryEnterReadOnly(TimeSpan,bool)"/>.
    /// </summary>
    /// <remarks>
    /// In DEBUG builds this method throws <see cref="MicroValueLockException"/> if no read lock
    /// is currently held. In Release builds the guard is elided for maximum throughput.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitReadOnly()
    {
#if DEBUG
        if (lockIndicator < FIRST_READ_LOCK)
        {
            if (lockIndicator == NO_LOCK) throw new MicroValueLockException("Exiting not acquired lock!");
            else throw new MicroValueLockException("Trying to exit write lock state with ExitReadLock() instead of Exit()!");
        }
#endif
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