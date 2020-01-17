using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace FeatureFlowFramework.Helper
{
    public class AsyncManualResetEvent2 : IValueTaskSource
    {
        ManualResetEventSlim mre;
        volatile int currentToken = 0;
        ConcurrentBag<ContinuationData> continuationDataStore = new ConcurrentBag<ContinuationData>();

        public AsyncManualResetEvent2(bool initialState = false)
        {
            this.mre = new ManualResetEventSlim(initialState);
        }

        public void Set()
        {
            if(!mre.IsSet)
            {
                mre.Set();

                while(continuationDataStore.TryTake(out ContinuationData data))
                {
                    InvokeContinuation(data);
                }
            }                        
        }

        public void Reset()
        {
            if(mre.IsSet)
            {
                var token = currentToken;
                var nextToken = token >= short.MaxValue ? short.MinValue : token + 1;
                Interlocked.CompareExchange(ref currentToken, nextToken, token);

                mre.Reset();                
            }
        }

        public ValueTask WaitAsync()
        {
            return new ValueTask(this, (short)currentToken);
        }

        public void Wait()
        {
            mre.Wait();
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return token != currentToken || mre.IsSet ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Pending;
        }

        public void GetResult(short token)
        {            
        }

        struct ContinuationData
        {
            public Action<object> continuation;
            public object state;
            public ExecutionContext executionContext;
            public object scheduler;

            public ContinuationData(Action<object> continuation, object state) : this()
            {
                this.continuation = continuation;
                this.state = state;
            }
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            ContinuationData data = new ContinuationData(continuation, state);

            if((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                data.executionContext = ExecutionContext.Capture();
            }

            if((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext sc = SynchronizationContext.Current;
                if(sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    data.scheduler = sc;
                }
                else
                {
                    TaskScheduler ts = TaskScheduler.Current;
                    if(ts != TaskScheduler.Default)
                    {
                        data.scheduler = ts;
                    }
                }
            }

            if(token == currentToken && !mre.IsSet) continuationDataStore.Add(data);
            else InvokeContinuation(data);
        }

        private void InvokeContinuation(ContinuationData data)
        {
            if(data.continuation == null) return;
                
            if(data.scheduler != null)
            {
                if(data.scheduler is SynchronizationContext sc)
                {
                    sc.Post(s =>
                    {
                        var t = (Tuple<Action<object>, object>)s;
                        t.Item1(t.Item2);
                    }, Tuple.Create(data.continuation, data.state));
                }
                else
                {
                    Debug.Assert(data.scheduler is TaskScheduler, $"Expected TaskScheduler, got {data.scheduler}");
                    Task.Factory.StartNew(data.continuation, data.state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, (TaskScheduler)data.scheduler);
                }
            }
            else
            {
                Task.Factory.StartNew(data.continuation, data.state, TaskCreationOptions.DenyChildAttach);
            }
        }
    }
}