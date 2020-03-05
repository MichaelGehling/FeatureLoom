using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class RpcCaller : IDataFlowSource, IDataFlowSink, IRequester
    {
        private DataFlowSourceHelper sourceHelper = new DataFlowSourceHelper();
        private List<IResponseHandler> responseHandlers = new List<IResponseHandler>();
        private readonly TimeSpan timeout;
        private readonly Timer timeoutTimer;

        public RpcCaller(TimeSpan timeout)
        {
            this.timeout = timeout;
            this.timeoutTimer = new Timer(CheckForTimeouts, null, timeout, timeout.Multiply(0.5));
        }

        public void CheckForTimeouts(object state)
        {
            lock (responseHandlers)
            {
                for (int i = 0; i < responseHandlers.Count; i++)
                {
                    if (responseHandlers[i].LifeTime.Elapsed)
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
            lock (responseHandlers)
            {
                responseHandlers.Add(new MultiResponseHandler<R>(requestId, responseSink, timeout));
            }
            sourceHelper.Forward(request);
        }

        public void CallMultiResponse<R>(string method, IDataFlowSink responseSink)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true);
            lock (responseHandlers)
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
            lock (responseHandlers)
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
            lock (responseHandlers)
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
            lock (responseHandlers)
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
            lock (responseHandlers)
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
            if (message is RpcErrorResponse errorResponse)
            {
                Log.ERROR(this, "RPC call failed!", errorResponse.ErrorMessage);
            }
            else if (message is IRpcResponse)
            {
                lock (responseHandlers)
                {
                    for (int i = 0; i < responseHandlers.Count; i++)
                    {
                        if (responseHandlers[i].Handle(message))
                        {
                            responseHandlers.RemoveAt(i--);
                            break;
                        }
                        else if (responseHandlers[i].LifeTime.Elapsed)
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

        public int CountConnectedSinks => ((IDataFlowSource)sourceHelper).CountConnectedSinks;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sourceHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sourceHelper).GetConnectedSinks();
        }

        public void ConnectToAndBack(IReplier replier)
        {
            this.ConnectTo(replier);
            replier.ConnectTo(this);
        }
    }
}