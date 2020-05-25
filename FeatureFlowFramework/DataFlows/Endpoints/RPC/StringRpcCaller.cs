using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Extensions;
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
    public partial class StringRpcCaller : IDataFlowSource, IDataFlowSink, IRequester
    {
        private DataFlowSourceHelper sourceHelper = new DataFlowSourceHelper();
        private List<IResponseHandler> responseHandlers = new List<IResponseHandler>();
        FeatureLock responseHandlersLock = new FeatureLock();
        private readonly TimeSpan timeout;
        private readonly Timer timeoutTimer;

        public int CountConnectedSinks => ((IDataFlowSource)sourceHelper).CountConnectedSinks;

        public StringRpcCaller(TimeSpan timeout)
        {
            this.timeout = timeout;
            this.timeoutTimer = new Timer(CheckForTimeouts, null, timeout, timeout.Multiply(0.5));
        }

        public void CheckForTimeouts(object state)
        {
            using (responseHandlersLock.ForWriting())
            {
                for(int i = 0; i < responseHandlers.Count; i++)
                {
                    if(responseHandlers[i].LifeTime.Elapsed)
                    {
                        responseHandlers[i].Cancel();
                        responseHandlers.RemoveAt(i--);
                    }
                }
            }
        }

        public void CallMultiResponse<P, R>(string method, P parameterTuple, IDataFlowSink sink)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<P, R>(requestId, method, parameterTuple);
            using (responseHandlersLock.ForWriting())
            {
                responseHandlers.Add(new MultiResponseHandler<R>(requestId, sink, timeout));
            }
            sourceHelper.Forward(request);
        }

        public void CallMultiResponse<R>(string method, IDataFlowSink sink)
        {
            var requestId = RandomGenerator.Int64();
            var request = new RpcRequest<bool, R>(requestId, method, true);
            using (responseHandlersLock.ForWriting())
            {
                responseHandlers.Add(new MultiResponseHandler<R>(requestId, sink, timeout));
            }
            sourceHelper.Forward(request);
        }

        public Task<string> CallAsync(string methodCall)
        {
            var requestId = RandomGenerator.Int64();
            string serializedRpcRequest = BuildJsonRpcRequest(methodCall, requestId, false);
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            using (responseHandlersLock.ForWriting())
            {
                responseHandlers.Add(new ResponseHandler(requestId, tcs, timeout));
            }
            sourceHelper.Forward(serializedRpcRequest);
            return tcs.Task;
        }

        public void CallNoResponse(string methodCall)
        {
            var requestId = RandomGenerator.Int64();
            string serializedRpcRequest = BuildJsonRpcRequest(methodCall, requestId, true);
            sourceHelper.Forward(serializedRpcRequest);
        }

        private string BuildJsonRpcRequest(string methodCall, long requestId, bool noResponse)
        {
            var parts = methodCall.Split(' ', 2);
            string methodName = parts[0];
            List<string> parameters = new List<string>();
            int openBraces = 0;
            bool openQuotationMark = false;
            string param = "";
            if(parts.Length > 1)
            {
                foreach(var c in parts[1])
                {
                    if(c == '"')
                    {
                        openQuotationMark = !openQuotationMark;
                        param += c;
                    }
                    else if(c == '{' && !openQuotationMark)
                    {
                        openBraces++;
                        param += c;
                    }
                    else if(openBraces > 0 && !openQuotationMark)
                    {
                        if(c == '}') openBraces--;
                        param += c;
                    }
                    else if(c == ' ' && !openQuotationMark)
                    {
                        parameters.Add(param);
                        param = "";
                    }
                    else
                    {
                        param += c;
                    }
                }
                if(!param.EmptyOrNull()) parameters.Add(param);
            }
            string parameterSet = CreateJsonParamString(parameters);
            string reqStr = $"{{method:\"{methodName}\", requestId:{requestId}, noResponse:{(noResponse ? "true" : "false")}, parameterSet:{parameterSet}}}";
            return reqStr;
        }

        private string CreateJsonParamString(List<string> parameters)
        {
            if(parameters.Count == 0) return "true";
            else if(parameters.Count == 1) return parameters[0];
            else
            {
                string result = "{";
                int paramCount = 1;
                foreach(string p in parameters)
                {
                    result += $"Item{paramCount++}:{p},";
                }
                result = result.TrimEnd(',') + '}';
                return result;
            }
        }

        public void Post<M>(in M message)
        {
            if(message is RpcErrorResponse errorResponse)
            {
                Log.ERROR(this.GetHandle(), "String-RPC call failed!", errorResponse.ErrorMessage);
            }
            else if(message is IRpcResponse)
            {
                using (responseHandlersLock.ForWriting())
                {
                    for(int i = 0; i < responseHandlers.Count; i++)
                    {
                        if(responseHandlers[i].Handle(message))
                        {
                            responseHandlers.RemoveAt(i--);
                            break;
                        }
                        else if(responseHandlers[i].LifeTime.Elapsed)
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

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sourceHelper).DisconnectAll();
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sourceHelper).GetConnectedSinks();
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference= false)
        {
            this.ConnectTo(replier, weakReference);
            replier.ConnectTo(this, weakReference);
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
        }
    }
}