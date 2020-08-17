using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class FeatureLock
    {
        const int NO_LOCK = 0;
        const int WRITE_LOCK = -1;

        const int NOT_ENTERED = 0;
        const int WRITE_ENTERED = -1;        

        public const int NO_SPIN_WAIT = 0;
        public const int ONLY_SPIN_WAIT = int.MaxValue;
        public const int BALANCED_SPIN_WAIT = 1;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;
        volatile bool writerPriority = false;


        bool supportReentrance;
        AsyncLocal<int> reentranceIndicator;
        int writingReentranceCounter = 0;

        int defaultFullSpinCycles;

        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

        public FeatureLock(int defaultFullSpinCycles = BALANCED_SPIN_WAIT, bool supportReentrance = false)
        {
            this.defaultFullSpinCycles = defaultFullSpinCycles;
            this.supportReentrance = supportReentrance;
            if (supportReentrance) reentranceIndicator = new AsyncLocal<int>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForReading(out ActiveLock readLock, TimeSpan timeout = default)
        {
            return TryForReading(defaultFullSpinCycles, out readLock, timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForReading(int fullSpinCyclesBeforeWait, out ActiveLock readLock, TimeSpan timeout = default)
        {
            var timer = new TimeFrame(timeout);
            readLock = new ActiveLock();

            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator) || writerPriority || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.Elapsed) return false;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    bool didReset = mre.Reset();
                    if (ReaderMustWait(lockIndicator))
                    {                        
                        if (!mre.Wait(timer.Remaining)) return false;                        
                        spinWait.Reset();
                        fullSpins++;
                    }
                    else if (didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }

            readLock = new ActiveLock(this, false);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForWriting(out ActiveLock writeLock, TimeSpan timeout = default)
        {
            return TryForWriting(defaultFullSpinCycles, out writeLock, timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForWriting(int fullSpinCyclesBeforeWait, out ActiveLock writeLock, TimeSpan timeout = default)
        {
            var timer = new TimeFrame(timeout);
            writeLock = new ActiveLock();

            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (timer.Elapsed)
                {
                    writerPriority = false;
                    return false;
                }
                if (currentLockIndicator > NO_LOCK) writerPriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    bool didReset = mre.Reset();
                    if (WriterMustWait(lockIndicator))
                    {
                        if (!mre.Wait(timer.Remaining))
                        {
                            writerPriority = false;
                            return false;
                        }
                        spinWait.Reset();
                        fullSpins++;
                    }
                    else if (didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            writerPriority = false;
            writeLock = new ActiveLock(this, true);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActiveLock ForReading()
        {
            return ForReading(defaultFullSpinCycles);
        }        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActiveLock ForReading(int fullSpinCyclesBeforeWait)
        {
            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator) || writerPriority || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    bool didReset = mre.Reset();
                    if(ReaderMustWait(lockIndicator))
                    {
                        mre.Wait();
                        spinWait.Reset();
                        fullSpins++;
                    }
                    else if(didReset) mre.Set();
                } 

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }

            return new ActiveLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<ActiveLock> ForReadingAsync()
        {
            return ForReadingAsync(defaultFullSpinCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<ActiveLock> ForReadingAsync(int fullSpinCyclesBeforeWait)
        {
            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator) || writerPriority || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {                
                if (fullSpins < fullSpinCyclesBeforeWait)
                {
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    bool didReset = mre.Reset();
                    if (ReaderMustWait(lockIndicator))
                    {
                        await mre.WaitAsync();
                        spinWait.Reset();
                        fullSpins++;
                    }
                    else if(didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            return new ActiveLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActiveLock ForWriting()
        {
            return ForWriting(defaultFullSpinCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActiveLock ForWriting(int fullSpinCyclesBeforeWait)
        {
            int currentReentranceIndicator = NOT_ENTERED;
            if (supportReentrance)
            {
                currentReentranceIndicator = reentranceIndicator.Value;
                if (currentReentranceIndicator == WRITE_ENTERED)
                {
                    writingReentranceCounter++;
                    return new ActiveLock(this, true);
                }
            }

            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (currentLockIndicator > NO_LOCK) writerPriority = true;
                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    bool didReset = mre.Reset();
                    if (WriterMustWait(lockIndicator))
                    {
                        mre.Wait();
                        spinWait.Reset();
                        fullSpins++;
                    }
                    else if(didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            writerPriority = false;

            if (supportReentrance)
            {
                writingReentranceCounter++;
                if (currentReentranceIndicator != WRITE_ENTERED) reentranceIndicator.Value = WRITE_ENTERED;
            }

            return new ActiveLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<ActiveLock> ForWritingAsync()
        {
            return ForWritingAsync(defaultFullSpinCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<ActiveLock> ForWritingAsync(int fullSpinCyclesBeforeWait)
        {
            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (currentLockIndicator > NO_LOCK) writerPriority = true;
                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    bool didReset = mre.Reset();
                    if (WriterMustWait(lockIndicator))
                    {
                        await mre.WaitAsync();
                        spinWait.Reset();
                        fullSpins++;
                    }
                    else if(didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            writerPriority = false;
            return new ActiveLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator)
        {
            return currentLockIndicator < NO_LOCK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator)
        {
            return currentLockIndicator != NO_LOCK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
            if (NO_LOCK == newLockIndicator)
            {
                mre.Set();
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            if (supportReentrance)
            {
                writingReentranceCounter--;
                if (writingReentranceCounter == 0) reentranceIndicator.Value = NOT_ENTERED;
                else return;
            }

            lockIndicator = NO_LOCK;
            mre.Set();
        }

        public struct ActiveLock : IDisposable
        {
            FeatureLock parentLock;
            readonly bool isWriteLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ActiveLock(FeatureLock parentLock, bool isWriteLock)
            {
                this.parentLock = parentLock;
                this.isWriteLock = isWriteLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Exit();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                if (isWriteLock) parentLock?.ExitWriteLock();
                else parentLock?.ExitReadLock();
                parentLock = null;
            }
        }

    }
}
