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
    public static class AsyncLockExtensions
    {
        private static ConditionalWeakTable<object, AsyncLock> lockObjects = new ConditionalWeakTable<object, AsyncLock>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncLock.ReadLock LockForReading<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForReading();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<AsyncLock.ReadLock> LockForReadingAsync<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForReadingAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncLock.WriteLock LockForWriting<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForWriting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<AsyncLock.WriteLock> LockForWritingAsync<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForWritingAsync();
        }
    }


    public class AsyncLock
    {

        const int NO_LOCKID = 0;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockId larger than NO_LOCKID (0) implies a write-lock, while a lockId smaller than NO_LOCKID implies a read-lock.
        /// When entering a read-lock, the lockId is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockId (greater than NO_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockId = NO_LOCKID;

        /// <summary>
        /// Used to generate write lock Ids. To generate a new id, it is incremented. 
        /// When exceeding the maximum value it is reset to NO_LOCKID.
        /// </summary>
        volatile int lockIdCounter = NO_LOCKID;
        TaskCompletionSource<bool> tcs;
        SpinWait spinWait = new SpinWait();
        ValueTaskSource vts;
        ManualResetEventSlim mre = new ManualResetEventSlim(true, 0);

        public AsyncLock()
        {
            vts = new ValueTaskSource(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading(bool onlySpinWaiting = false)
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if (currentLockId > NO_LOCKID)
                {
                    if(onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    else
                    {
                        if(lockId == currentLockId) mre.Reset();
                        mre.Wait();
                        currentLockId = lockId;
                    }
                    currentLockId = lockId;
                }
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));

            ResetTcs();

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetTcs()
        {
            bool ok = true;
            do
            {
                ok = true;
                var currentTCS = tcs;
                if(currentTCS != null && currentTCS.Task.IsCompleted) ok = currentTCS == Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), currentTCS);
            }
            while(!ok);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<ReadLock> ForReadingAsync(bool onlySpinWaiting = false)
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if (currentLockId > NO_LOCKID)
                {
                    if (onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    await vts.WaitForReadingAsync();
                    currentLockId = lockId;
                }
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));

            ResetTcs();

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting(bool onlySpinWaiting = false)
        {
            var newLockId = Interlocked.Increment(ref lockIdCounter);
            while(newLockId <= NO_LOCKID)
            {
                Interlocked.CompareExchange(ref lockIdCounter, NO_LOCKID, newLockId);
                newLockId = Interlocked.Increment(ref lockIdCounter);
            }

            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if(currentLockId != NO_LOCKID)
                {
                    if (onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    else
                    {
                        if (lockId == currentLockId) mre.Reset();
                        mre.Wait();
                        currentLockId = lockId;
                    }
                    currentLockId = lockId;
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));

            ResetTcs();

            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<WriteLock> ForWritingAsync(bool onlySpinWaiting = false)
        {
            var newLockId = Interlocked.Increment(ref lockIdCounter);
            while(newLockId <= NO_LOCKID)
            {
                Interlocked.CompareExchange(ref lockIdCounter, NO_LOCKID, newLockId);
                newLockId = Interlocked.Increment(ref lockIdCounter);
            }

            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if(currentLockId != NO_LOCKID)
                {
                    if (onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    await vts.WaitForWritingAsync();
                    currentLockId = lockId;
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));

            ResetTcs();

            return new WriteLock(this);
        }

        

        public struct ReadLock : IDisposable
        {
            AsyncLock safeLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadLock(AsyncLock safeLock)
            {
                this.safeLock = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                var newLockId = Interlocked.Increment(ref safeLock.lockId);

                if (NO_LOCKID == newLockId)
                {
                    safeLock.spinWait.Reset();
                    if(!safeLock.mre.IsSet) safeLock.mre.Set();
                    safeLock.vts.Complete();
                }
            }
        }

        public struct WriteLock : IDisposable
        {
            AsyncLock safeLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WriteLock(AsyncLock safeLock)
            {
                this.safeLock = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                safeLock.spinWait.Reset();
                safeLock.lockId = NO_LOCKID;
                if (!safeLock.mre.IsSet) safeLock.mre.Set();
                safeLock.vts.Complete();
            }
        }

        public class ValueTaskSource : IValueTaskSource
        {
            AsyncLock parent;
            const int maxTasks = 100;
            ValueTaskData[] valueTaskDataSet = new ValueTaskData[maxTasks];
            volatile int lastToken = -1;
            volatile int nextCompletion = 0;

            struct ValueTaskData
            {
                public readonly short token;
                public readonly bool reading;
                public readonly bool initialized;
                public bool completed;
                public ContinuationData continuationData;

                public ValueTaskData(short token, bool reading)
                {
                    this.token = token;
                    this.reading = reading;
                    completed = false;
                    this.continuationData = default;
                    this.initialized = true;
                }

                public ValueTaskData WithContinuation(ContinuationData data)
                {
                    continuationData = data;
                    return this;
                }

                public ValueTaskData Complete()
                {
                    completed = true;
                    return this;
                }

                public bool Continue()
                {
                    if(continuationData.initialized)
                    {
                        continuationData.Continue();
                        return true;
                    }
                    else return false;
                }
            }
            

            public ValueTaskSource(AsyncLock parent)
            {
                this.parent = parent;
            }

            public ValueTask WaitForReadingAsync()
            {
                var token = Interlocked.Increment(ref lastToken);
                if(token == maxTasks-1) lastToken = -1;
                else if (token >= maxTasks-1) token = Interlocked.Increment(ref lastToken);
                short shortToken = (short)token;
                valueTaskDataSet[token] = new ValueTaskData(shortToken, true);
                return new ValueTask(this, shortToken);
            }

            public ValueTask WaitForWritingAsync()
            {
                var token = Interlocked.Increment(ref lastToken);
                if(token == maxTasks - 1) lastToken = -1;
                else if(token >= maxTasks - 1) token = Interlocked.Increment(ref lastToken);
                short shortToken = (short)token;
                valueTaskDataSet[token] = new ValueTaskData(shortToken, false);
                return new ValueTask(this, shortToken);
            }

            public void GetResult(short token)
            {
                var data = valueTaskDataSet[token];
                if(!data.initialized) throw new Exception("Invalid token!");
                if(data.completed) valueTaskDataSet[token] = new ValueTaskData();
                return;
            }

            public ValueTaskSourceStatus GetStatus(short token)
            {
                return valueTaskDataSet[token].completed ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Pending;
            }

            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                var data = valueTaskDataSet[token];
                if (data.completed) continuation(state);
                else
                {
                    object scheduler = null;
                    SynchronizationContext sc = SynchronizationContext.Current;
                    if(sc != null && sc.GetType() != typeof(SynchronizationContext))
                    {
                        scheduler = sc;
                    }
                    else
                    {
                        TaskScheduler ts = TaskScheduler.Current;
                        if(ts != TaskScheduler.Default)
                        {
                            scheduler = ts;
                        }
                    }

                    var continuationData = new ContinuationData(continuation, state, token, scheduler);
                    valueTaskDataSet[token] = data.WithContinuation(continuationData);
                }
            }


            public void Complete()
            {
                var from = nextCompletion;
                var to = lastToken;
                while ((from <= to || from > to + 1))
                {
                    var i = nextCompletion++;
                    if(nextCompletion >= maxTasks) nextCompletion = 0;

                    var data = valueTaskDataSet[i];
                    if(!data.initialized) continue;
                    if(data.continuationData.initialized)
                    {
                        data.continuationData.Continue();
                        valueTaskDataSet[i] = new ValueTaskData();
                    }
                    else valueTaskDataSet[i] = data.Complete();

                    from = nextCompletion;
                    to = lastToken;
                }
            }

            struct ContinuationData
            {
                public Action<object> continuation;
                public object state;
                public short token;
                public object scheduler;
                public bool initialized;

                public ContinuationData(Action<object> continuation, object state, short token, object syncContext)
                {
                    this.continuation = continuation;
                    this.state = state;
                    this.token = token;
                    this.scheduler = syncContext;
                    this.initialized = true;
                }

                public void Continue()
                {
                    if(scheduler != null)
                    {
                        if(scheduler is SynchronizationContext sc)
                        {
                            sc.Post(s =>
                            {
                                (Action<object> execute, object state) t = ((Action<object> execute, object state))s;
                                t.execute(t.state);
                            }, (continuation, state));
                        }
                        else if(scheduler is TaskScheduler ts)
                        {
                            Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                        }
                    }
                    else
                    {
                        continuation(state);
                    }
                }
            }
        }
    }
}
