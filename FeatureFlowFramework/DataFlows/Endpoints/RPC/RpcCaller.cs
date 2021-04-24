using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Services;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Services.MetaData;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class RpcCaller : IDataFlowSource, IDataFlowSink, IRequester
    {
        private SourceValueHelper sourceHelper;
        private List<IResponseHandler> responseHandlers = new List<IResponseHandler>();
        FeatureLock responseHandlersLock = new FeatureLock();
        private readonly TimeSpan timeout;
        private readonly Timer timeoutTimer;

        public RpcCaller(TimeSpan timeout)
        {
            this.timeout = timeout;
            this.timeoutTimer = new Timer(CheckForTimeouts, null, timeout, timeout.Multiply(0.5));
        }

        public void CheckForTimeouts(object state)
        {
            DateTime now = AppTime.Now;
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

        public void CallMultiResponse<P, R>(string method, P parameterTuple, IDataFlowSink responseSink)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, R>(requestId, method, parameterTuple);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new MultiResponseHandler<R>(requestId, responseSink, timeout));
            }
            sourceHelper.Forward(request);
        }

        public void CallMultiResponse<R>(string method, IDataFlowSink responseSink)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true);
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new MultiResponseHandler<R>(requestId, responseSink, timeout));
            }
            sourceHelper.Forward(request);
        }

        public Task<R> CallAsync<P, R>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, R>(requestId, method, parameterTuple);
            TaskCompletionSource<R> tcs = new TaskCompletionSource<R>();
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<R>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(request);
            return tcs.Task;
        }

        public Task CallAsync<P>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, bool>(requestId, method, parameterTuple);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<bool>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(request);
            return tcs.Task;
        }

        public Task<R> CallAsync<R>(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true);
            TaskCompletionSource<R> tcs = new TaskCompletionSource<R>();
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<R>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(request);
            return tcs.Task;
        }

        public Task CallAsync(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, bool>(requestId, method, true);
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            using (responseHandlersLock.Lock())
            {
                responseHandlers.Add(new ResponseHandler<bool>(requestId, tcs, timeout));
            }
            sourceHelper.Forward(request);
            return tcs.Task;
        }

        public void CallNoResponse<P, R>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, R>(requestId, method, parameterTuple, true);
            sourceHelper.Forward(request);
        }

        public void CallNoResponse<P>(string method, P parameterTuple)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, bool>(requestId, method, parameterTuple, true);
            sourceHelper.Forward(request);
        }

        public void CallNoResponse<R>(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true, true);
            sourceHelper.Forward(request);
        }

        public void CallNoResponse(string method)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, bool>(requestId, method, true, true);
            sourceHelper.Forward(request);
        }

        public void Post<M>(in M message)
        {
            if(message is RpcErrorResponse errorResponse)
            {
                Log.ERROR(this.GetHandle(), "RPC call failed!", errorResponse.ErrorMessage);
            }
            else if(message is IRpcResponse)
            {
                DateTime now = AppTime.Now;
                using (responseHandlersLock.Lock())
                {
                    for(int i = 0; i < responseHandlers.Count; i++)
                    {
                        if(responseHandlers[i].Handle(message))
                        {
                            responseHandlers.RemoveAt(i--);
                            break;
                        }
                        else if(responseHandlers[i].LifeTime.Elapsed(now))
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

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            this.ConnectTo(replier, weakReference);
            replier.ConnectTo(this, weakReference);
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }
}