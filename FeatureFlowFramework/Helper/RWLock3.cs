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
    public class RWLock3
    {
        const int NO_LOCKID = 0;
        const int WRITE_LOCKID = NO_LOCKID + 1;
        const long NO_WAITING = long.MaxValue;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockId larger than NO_LOCKID (0) implies a write-lock, while a lockId smaller than NO_LOCKID implies a read-lock.
        /// When entering a read-lock, the lockId is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockId (greater than NO_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockId = NO_LOCKID;
        volatile bool writePriority = false;
        volatile bool readPriority = false;

        int defaultFullSpinCycles = 1;

        AsyncManualResetEvent mreReader = new AsyncManualResetEvent(true);
        AsyncManualResetEvent mreWriter = new AsyncManualResetEvent(true);

        public RWLock3(int defaultFullSpinCycles = 1)
        {
            this.defaultFullSpinCycles = defaultFullSpinCycles;
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
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;            
            while (ReaderMustWait(currentLockId) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                if (currentLockId > NO_LOCKID || fullSpins > 0) readPriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    readPriority = true;
                    bool didReset = mreReader.Reset();
                    if(ReaderMustWait(lockId))
                    {
                        mreReader.Wait();
                        spinWait.Reset();
                        fullSpins++;
                        readPriority = true;
                    }
                    else if(didReset) mreReader.Set();
                } 

                currentLockId = lockId;
                newLockId = currentLockId - 1;
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
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;
            while (ReaderMustWait(currentLockId) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                if (currentLockId > NO_LOCKID || fullSpins > 0) readPriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    readPriority = true;
                    bool didReset = mreReader.Reset();
                    if (ReaderMustWait(lockId))
                    {
                        await mreReader.WaitAsync();
                        spinWait.Reset();
                        fullSpins++;
                        readPriority = true;
                    }
                    else if(didReset) mreReader.Set();
                }

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            readPriority = false;

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockId)
        {
            return currentLockId > NO_LOCKID || (writePriority && !readPriority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockId = Interlocked.Increment(ref lockId);
            if (NO_LOCKID == newLockId)
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
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {
                if (currentLockId < NO_LOCKID || fullSpins > 0) writePriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    writePriority = true;
                    bool didReset = mreWriter.Reset();
                    if (WriterMustWait(lockId))
                    {
                        mreWriter.Wait();
                        spinWait.Reset();
                        fullSpins++;
                        writePriority = true;
                    }
                    else if(didReset) mreWriter.Set();
                }

                currentLockId = lockId;
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
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {
                if (currentLockId < NO_LOCKID || fullSpins > 0) writePriority = true;

                if (fullSpins < fullSpinCyclesBeforeWait)
                {                    
                    spinWait.SpinOnce();
                    if (spinWait.NextSpinWillYield) fullSpins++;
                }
                else
                {
                    writePriority = true;
                    bool didReset = mreWriter.Reset();
                    if (WriterMustWait(lockId))
                    {
                        await mreWriter.WaitAsync();
                        spinWait.Reset();
                        fullSpins++;
                        writePriority = true;
                    }
                    else if(didReset) mreWriter.Set();
                }

                currentLockId = lockId;
            }
            writePriority = false;
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockId)
        {
            return currentLockId != NO_LOCKID || (readPriority && !writePriority);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockId = NO_LOCKID;
            if (!mreReader.Set()) mreWriter.Set();
        }

        public struct ReadLock : IDisposable
        {
            RWLock3 lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadLock(RWLock3 safeLock)
            {
                this.lockObj = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                lockObj.ExitReadLock();
            }
        }

        public struct WriteLock : IDisposable
        {
            RWLock3 lockObj;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WriteLock(RWLock3 safeLock)
            {
                this.lockObj = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                lockObj.ExitWriteLock();
            }
        }
    }
}
