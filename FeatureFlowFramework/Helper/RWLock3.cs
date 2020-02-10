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
        long longestWaitingReader = NO_WAITING;
        long longestWaitingWriter = NO_WAITING;

        AsyncManualResetEvent mreReader = new AsyncManualResetEvent(true);
        AsyncManualResetEvent mreWriter = new AsyncManualResetEvent(true);


        SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.Balanced;

        public RWLock3(SpinWaitBehaviour defaultSpinningBehaviour = SpinWaitBehaviour.Balanced)
        {
            this.defaultSpinningBehaviour = defaultSpinningBehaviour;
        }

        public enum SpinWaitBehaviour
        {
            Balanced,
            NoSpinning,
            OnlySpinning
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading()
        {
            return ForReading(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading(SpinWaitBehaviour spinWaitBehaviour)
        {
            long myWaitStart = NO_WAITING;
            SpinWait spinWait = new SpinWait();
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;            
            while (ReaderMustWait(currentLockId, myWaitStart) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                if(myWaitStart == NO_WAITING) myWaitStart = AppTime.Elapsed.Ticks;
                if(myWaitStart < Thread.VolatileRead(ref longestWaitingReader)) Thread.VolatileWrite(ref longestWaitingReader, myWaitStart);

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && mreReader.IsSet && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    bool didReset = mreReader.Reset(); ;
                    if(ReaderMustWait(lockId, myWaitStart))
                    {
                        mreReader.Wait();
                        spinWait.Reset();
                    }
                    else if(didReset) mreReader.Set();
                } 

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            if (myWaitStart != NO_WAITING && myWaitStart <= Thread.VolatileRead(ref longestWaitingReader)) Thread.VolatileWrite(ref longestWaitingReader, NO_WAITING);
            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<ReadLock> ForReadingAsync()
        {
            return ForReadingAsync(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<ReadLock> ForReadingAsync(SpinWaitBehaviour spinWaitBehaviour)
        {
            long myWaitStart = NO_WAITING;
            SpinWait spinWait = new SpinWait();
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;
            while (ReaderMustWait(currentLockId, myWaitStart) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                if(myWaitStart == NO_WAITING) myWaitStart = AppTime.Elapsed.Ticks;
                if(myWaitStart < Thread.VolatileRead(ref longestWaitingReader)) Thread.VolatileWrite(ref longestWaitingReader, myWaitStart);

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && mreReader.IsSet && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    bool didReset = mreReader.Reset();
                    if (ReaderMustWait(lockId, myWaitStart))
                    {
                        await mreReader.WaitAsync();
                        spinWait.Reset();
                    }
                    else if(didReset) mreReader.Set();
                }

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            if (myWaitStart != NO_WAITING && myWaitStart <= Thread.VolatileRead(ref longestWaitingReader)) Thread.VolatileWrite(ref longestWaitingReader, NO_WAITING);
            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockId, long myWaitStart)
        {
            //return currentLockId > NO_LOCKID || (currentLockId < NO_LOCKID && Thread.VolatileRead(ref longestWaitingReader) > Thread.VolatileRead(ref longestWaitingWriter));
            return currentLockId > NO_LOCKID || Thread.VolatileRead(ref longestWaitingReader) > Thread.VolatileRead(ref longestWaitingWriter);            
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
            return ForWriting(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting(SpinWaitBehaviour spinWaitBehaviour)
        {
            long myWaitStart = NO_WAITING;
            SpinWait spinWait = new SpinWait();
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId, myWaitStart) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {
                if(myWaitStart == NO_WAITING) myWaitStart = AppTime.Elapsed.Ticks;
                if(myWaitStart < Thread.VolatileRead(ref longestWaitingWriter)) Thread.VolatileWrite(ref longestWaitingWriter, myWaitStart);

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && mreWriter.IsSet && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    bool didReset = mreWriter.Reset(); ;
                    if (WriterMustWait(lockId, myWaitStart))
                    {
                        mreWriter.Wait();
                        spinWait.Reset();
                        if (myWaitStart != Thread.VolatileRead(ref longestWaitingWriter)) spinWait.SpinOnce();
                    }
                    else if(didReset) mreWriter.Set();
                }

                currentLockId = lockId;
            }
            if (myWaitStart != NO_WAITING && myWaitStart <= Thread.VolatileRead(ref longestWaitingWriter)) Thread.VolatileWrite(ref longestWaitingWriter, NO_WAITING);
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<WriteLock> ForWritingAsync()
        {
            return ForWritingAsync(defaultSpinningBehaviour);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<WriteLock> ForWritingAsync(SpinWaitBehaviour spinWaitBehaviour)
        {
            long myWaitStart = NO_WAITING;
            SpinWait spinWait = new SpinWait();
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId, myWaitStart) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {
                if(myWaitStart == NO_WAITING) myWaitStart = AppTime.Elapsed.Ticks;
                if(myWaitStart < Thread.VolatileRead(ref longestWaitingWriter)) Thread.VolatileWrite(ref longestWaitingWriter, myWaitStart);

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && mreWriter.IsSet && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    bool didReset = mreWriter.Reset();
                    if (WriterMustWait(lockId, myWaitStart))
                    {
                        await mreWriter.WaitAsync();
                        spinWait.Reset();
                        if (myWaitStart != Thread.VolatileRead(ref longestWaitingWriter)) spinWait.SpinOnce();
                    }
                    else if(didReset) mreWriter.Set();
                }

                currentLockId = lockId;
            }
            if (myWaitStart != NO_WAITING && myWaitStart <= Thread.VolatileRead(ref longestWaitingWriter)) Thread.VolatileWrite(ref longestWaitingWriter, NO_WAITING);
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockId, long myWaitStart)
        {
            return currentLockId != NO_LOCKID;
            //return currentLockId != NO_LOCKID || Thread.VolatileRead(ref longestWaitingReader) < Thread.VolatileRead(ref longestWaitingWriter);
            //return currentLockId != NO_LOCKID || (Thread.VolatileRead(ref longestWaitingReader) < myWaitStart && mreWriter.IsSet);

            /*if(currentLockId != NO_LOCKID) return true;
            else if(Thread.VolatileRead(ref longestWaitingReader) < myWaitStart)
            {
                if(!mreWriter.IsSet) mreWriter.Set();
                return true;
            }
            else return false;*/
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
