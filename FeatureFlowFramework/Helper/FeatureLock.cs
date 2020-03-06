using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace FeatureFlowFramework.Helper
{
    public class FeatureLock
    {
        const int NO_LOCK = 0;
        const int WRITE_LOCK = NO_LOCK + 1;

        public const int NO_SPIN_WAIT = 0;
        public const int ONLY_SPIN_WAIT = int.MaxValue;
        public const int BALANCED_SPIN_WAIT = 1;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator larger than NO_LOCK (0) implies a write-lock, while a lockIndicator smaller than NO_LOCK implies a read-lock.
        /// When entering a read-lock, the lockIndicator is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockIndicator (WRITE_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;
        volatile bool writePriority = false;
        volatile bool readPriority = false;

        int defaultFullSpinCycles;

        AsyncManualResetEvent mreReader = new AsyncManualResetEvent(true);
        AsyncManualResetEvent mreWriter = new AsyncManualResetEvent(true);

        public FeatureLock(int defaultFullSpinCycles = BALANCED_SPIN_WAIT)
        {
            this.defaultFullSpinCycles = defaultFullSpinCycles;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForReading(out ReadLock readLock, TimeSpan timeout = default)
        {
            return TryForReading(defaultFullSpinCycles, out readLock, timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForReading(int fullSpinCyclesBeforeWait, out ReadLock readLock, TimeSpan timeout = default)
        {
            var timer = new TimeFrame(timeout);
            readLock = new ReadLock(null);

            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator - 1;
            while (ReaderMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.Elapsed) return false;

                if (currentLockIndicator > NO_LOCK) readPriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    readPriority = true;
                    bool didReset = mreReader.Reset();
                    if (ReaderMustWait(lockIndicator))
                    {
                        if (!mreReader.Wait(timer.Remaining)) return false;                        
                        spinWait.Reset();
                        fullSpins++;
                        readPriority = true;
                    }
                    else if (didReset) mreReader.Set();
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator - 1;
            }
            readPriority = false;

            readLock = new ReadLock(this);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading()
        {
            return ForReading(defaultFullSpinCycles);
        }        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading(int fullSpinCyclesBeforeWait)
        {
            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator - 1;            
            while (ReaderMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (currentLockIndicator > NO_LOCK) readPriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    readPriority = true;
                    bool didReset = mreReader.Reset();
                    if(ReaderMustWait(lockIndicator))
                    {
                        mreReader.Wait();
                        spinWait.Reset();
                        fullSpins++;
                        readPriority = true;
                    }
                    else if(didReset) mreReader.Set();
                } 

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator - 1;
            }
            readPriority = false;

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<ReadLock> ForReadingAsync()
        {
            return ForReadingAsync(defaultFullSpinCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<ReadLock> ForReadingAsync(int fullSpinCyclesBeforeWait)
        {
            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator - 1;
            while (ReaderMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (currentLockIndicator > NO_LOCK) readPriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    readPriority = true;
                    bool didReset = mreReader.Reset();
                    if (ReaderMustWait(lockIndicator))
                    {
                        await mreReader.WaitAsync();
                        spinWait.Reset();
                        fullSpins++;
                        readPriority = true;
                    }
                    else if(didReset) mreReader.Set();
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator - 1;
            }
            readPriority = false;

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator)
        {
            return currentLockIndicator > NO_LOCK || (writePriority && !readPriority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockIndicator = Interlocked.Increment(ref lockIndicator);
            if (NO_LOCK == newLockIndicator)
            {
                if (!mreWriter.Set()) mreReader.Set();               
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting()
        {
            return ForWriting(defaultFullSpinCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting(int fullSpinCyclesBeforeWait)
        {
            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (currentLockIndicator < NO_LOCK || fullSpins > 0) writePriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    writePriority = true;
                    bool didReset = mreWriter.Reset();
                    if (WriterMustWait(lockIndicator))
                    {
                        mreWriter.Wait();
                        spinWait.Reset();
                        fullSpins++;
                        writePriority = true;
                    }
                    else if(didReset) mreWriter.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            writePriority = false;
            
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<WriteLock> ForWritingAsync()
        {
            return ForWritingAsync(defaultFullSpinCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<WriteLock> ForWritingAsync(int fullSpinCyclesBeforeWait)
        {
            int fullSpins = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (currentLockIndicator < NO_LOCK || fullSpins > 0) writePriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    writePriority = true;
                    bool didReset = mreWriter.Reset();
                    if (WriterMustWait(lockIndicator))
                    {
                        await mreWriter.WaitAsync();
                        spinWait.Reset();
                        fullSpins++;
                        writePriority = true;
                    }
                    else if(didReset) mreWriter.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            writePriority = false;
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator)
        {
            return currentLockIndicator != NO_LOCK || (readPriority && !writePriority);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockIndicator = NO_LOCK;
            if (!mreReader.Set()) mreWriter.Set();
        }

        public struct ReadLock : IDisposable
        {
            FeatureLock parentLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadLock(FeatureLock parentLock)
            {
                this.parentLock = parentLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                parentLock?.ExitReadLock();
                parentLock = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                Dispose();
            }
        }

        public struct WriteLock : IDisposable
        {
            FeatureLock parentLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WriteLock(FeatureLock parentLock)
            {
                this.parentLock = parentLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                parentLock?.ExitWriteLock();
                parentLock = null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                Dispose();
            }
        }

    }
}
