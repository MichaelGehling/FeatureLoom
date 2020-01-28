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

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockId larger than NO_LOCKID (0) implies a write-lock, while a lockId smaller than NO_LOCKID implies a read-lock.
        /// When entering a read-lock, the lockId is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockId (greater than NO_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockId = NO_LOCKID;
        volatile int maxReadPressure = 0;
        volatile int maxWritePressure = 0;
        volatile int blockedReader = 0;
        volatile int blockedWriter = 0;

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
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;            
            while (ReaderMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                myPressure++;
                if (myPressure > maxReadPressure) maxReadPressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    Interlocked.Increment(ref blockedReader);
                    currentLockId = lockId;
                    if (ReaderMustWait(currentLockId, int.MaxValue))
                    {
                        if (mreReader.IsSet) mreReader.Reset();
                        mreReader.Wait();
                        spinWait.Reset();
                    }
                    else spinWait.SpinOnce();
                    Interlocked.Decrement(ref blockedReader);                    
                } 

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            maxReadPressure = 0;
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
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var currentLockId = lockId;
            var newLockId = currentLockId - 1;
            while (ReaderMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId))
            {
                myPressure++;
                if (myPressure > maxReadPressure) maxReadPressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    Interlocked.Increment(ref blockedReader);
                    currentLockId = lockId;
                    if (ReaderMustWait(currentLockId, int.MaxValue))
                    {
                        if (mreReader.IsSet) mreReader.Reset();
                        await mreReader.WaitAsync();
                        spinWait.Reset();
                    }
                    else spinWait.SpinOnce();
                    Interlocked.Decrement(ref blockedReader);
                }

                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            maxReadPressure = 0;
            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockId, int myPressure)
        {
            return currentLockId > NO_LOCKID || (currentLockId < NO_LOCKID && maxWritePressure >= maxReadPressure) || myPressure < maxReadPressure;
            //return currentLockId > NO_LOCKID || (currentLockId < NO_LOCKID && maxWritePressure >= 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockId = Interlocked.Increment(ref lockId);
            if (NO_LOCKID == newLockId)
            {
                if (blockedWriter > 0) mreWriter.Set();
                else if (blockedReader > 0) mreReader.Set();
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
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {

                myPressure++;
                if (myPressure > maxWritePressure) maxWritePressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    Interlocked.Increment(ref blockedWriter);
                    currentLockId = lockId;
                    if (WriterMustWait(currentLockId, int.MaxValue))
                    {
                        if (mreWriter.IsSet) mreWriter.Reset();
                        mreWriter.Wait();
                        spinWait.Reset();
                    }
                    else spinWait.SpinOnce();
                    Interlocked.Decrement(ref blockedWriter);
                }

                currentLockId = lockId;
            }
            maxWritePressure = 0;
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
            SpinWait spinWait = new SpinWait();
            int myPressure = 0;
            var newLockId = WRITE_LOCKID;
            var currentLockId = lockId;
            while (WriterMustWait(currentLockId, myPressure) || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID))
            {
                myPressure++;
                if (myPressure > maxWritePressure) maxWritePressure = myPressure;

                if (spinWaitBehaviour == SpinWaitBehaviour.OnlySpinning ||
                    (spinWaitBehaviour == SpinWaitBehaviour.Balanced && !spinWait.NextSpinWillYield))
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    Interlocked.Increment(ref blockedWriter);
                    currentLockId = lockId;
                    if (WriterMustWait(currentLockId, int.MaxValue))
                    {
                        if (mreWriter.IsSet) mreWriter.Reset();
                        await mreWriter.WaitAsync();
                        spinWait.Reset();
                    }
                    else spinWait.SpinOnce();
                    Interlocked.Decrement(ref blockedWriter);
                }

                currentLockId = lockId;
            }
            maxWritePressure = 0;
            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockId, int myPressure)
        {
            return currentLockId != NO_LOCKID || myPressure < maxWritePressure;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockId = NO_LOCKID;
            if (blockedReader > 0) mreReader.Set();            
            else if (blockedWriter > 0) mreWriter.Set();
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
