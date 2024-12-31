using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Scheduling;
using FeatureLoom.DependencyInversion;

namespace FeatureLoom.RPC
{
    public partial class RpcCaller : IMessageSource, IMessageSink, IRequester
    {
        private SourceValueHelper sourceHelper;
        private List<IResponseHandler> responseHandlers = new List<IResponseHandler>();
        private FeatureLock responseHandlersLock = new FeatureLock();
        private readonly TimeSpan timeout;
        private readonly ISchedule timeoutSchedule;

        public RpcCaller(TimeSpan timeout)
        {
            this.timeout = timeout;
            timeoutSchedule = ((Action<DateTime>)CheckForTimeouts).ScheduleForRecurringExecution("RpcCaller_Timeout", timeout.Multiply(0.5));            
        }

        public void CheckForTimeouts(DateTime now)
        {
            if (responseHandlers.Count == 0) return;

            using (responseHandlersLock.Lock())
            {
                for (int i = 0; i < responseHandlers.Count; i++)
                {
                    if (responseHandlers[i].LifeTime.Elapsed(now))
                    {
                        responseHandlers[i].Cancel();
                        responseHandlers.RemoveAt(i--);
                    }
                }
            }
        }

        public void CallMultiResponse<P, R>(string method, P parameterTuple, IMessageSink responseSink)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, R>(requestId, method, parameterTuple);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new MultiResponseHandler<R>(requestId, responseSink, timeout));
            }
            sourceHelper.Forward(in request);
        }

        public void CallMultiResponse<R>(string method, IMessageSink responseSink)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new MultiResponseHandler<R>(requestId, responseSink, timeout));
            }
            sourceHelper.Forward(in request);
        }

        public Task<R> CallAsync<P, R>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, R>(requestId, method, parameterTuple);
            TaskCompletionSource<R> tcs = new TaskCompletionSource<R>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<R>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(in request);
            return tcs.Task;
        }

        public Task CallAsync<P>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, bool>(requestId, method, parameterTuple);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<bool>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(in request);
            return tcs.Task;
        }

        public Task<R> CallAsync<R>(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true);
            TaskCompletionSource<R> tcs = new TaskCompletionSource<R>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<R>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(in request);
            return tcs.Task;
        }

        public Task CallAsync(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, bool>(requestId, method, true);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<bool>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(in request);
            return tcs.Task;
        }

        public void CallNoResponse<P, R>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, R>(requestId, method, parameterTuple, true);
            sourceHelper.Forward(in request);
        }

        public void CallNoResponse<P>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, bool>(requestId, method, parameterTuple, true);
            sourceHelper.Forward(in request);
        }

        public void CallNoResponse<R>(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true, true);
            sourceHelper.Forward(in request);
        }

        public void CallNoResponse(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, bool>(requestId, method, true, true);
            sourceHelper.Forward(in request);
        }

        public void Post<M>(in M message)
        {
            if (message is RpcErrorResponse errorResponse)
            {
                Log.ERROR(this.GetHandle(), "RPC call failed!", errorResponse.ErrorMessage);
            }
            else if (message is IRpcResponse)
            {
                DateTime now = AppTime.Now;
                using (responseHandlersLock.Lock())
                {
                    for (int i = 0; i < responseHandlers.Count; i++)
                    {
                        if (responseHandlers[i].Handle(message))
                        {
                            responseHandlers.RemoveAt(i--);
                            break;
                        }
                        else if (responseHandlers[i].LifeTime.Elapsed(now))
                        {
                            responseHandlers[i].Cancel();
                            responseHandlers.RemoveAt(i--);
                        }
                    }
                }
            }
        }

        public void Post<M>(M message)
        {
            if (message is RpcErrorResponse errorResponse)
            {
                Log.ERROR(this.GetHandle(), "RPC call failed!", errorResponse.ErrorMessage);
            }
            else if (message is IRpcResponse)
            {
                DateTime now = AppTime.Now;
                using (responseHandlersLock.Lock())
                {
                    for (int i = 0; i < responseHandlers.Count; i++)
                    {
                        if (responseHandlers[i].Handle(message))
                        {
                            responseHandlers.RemoveAt(i--);
                            break;
                        }
                        else if (responseHandlers[i].LifeTime.Elapsed(now))
                        {
                            responseHandlers[i].Cancel();
                            responseHandlers.RemoveAt(i--);
                        }
                    }
                }
            }
        }

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            this.ConnectTo(replier, weakReference);
            replier.ConnectTo(this, weakReference);
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }
}