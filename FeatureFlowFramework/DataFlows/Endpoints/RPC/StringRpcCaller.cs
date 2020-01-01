using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public class StringRpcCaller : IDataFlowSource, IDataFlowSink, IRequester
    {
        private DataFlowSourceHelper sourceHelper = new DataFlowSourceHelper();
        private List<IResponseHandler> responseHandlers = new List<IResponseHandler>();
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

        public Task<string> CallAsync(string methodCall)
        {
            var requestId = RandomGenerator.Int64;
            string serializedRpcRequest = BuildJsonRpcRequest(methodCall, requestId, false);
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
            lock (responseHandlers)
            {
                responseHandlers.Add(new ResponseHandler(requestId, tcs, timeout));
            }
            sourceHelper.Forward(serializedRpcRequest);
            return tcs.Task;
        }

        public void CallNoResponse(string methodCall)
        {
            var requestId = RandomGenerator.Int64;
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
            if (parts.Length > 1)
            {
                foreach (var c in parts[1])
                {
                    if (c == '"')
                    {
                        openQuotationMark = !openQuotationMark;
                        param += c;
                    }
                    else if (c == '{' && !openQuotationMark)
                    {
                        openBraces++;
                        param += c;
                    }
                    else if (openBraces > 0 && !openQuotationMark)
                    {
                        if (c == '}') openBraces--;
                        param += c;
                    }
                    else if (c == ' ' && !openQuotationMark)
                    {
                        parameters.Add(param);
                        param = "";
                    }
                    else
                    {
                        param += c;
                    }
                }
                if (!param.EmptyOrNull()) parameters.Add(param);
            }
            string parameterSet = CreateJsonParamString(parameters);
            string reqStr = $"{{method:\"{methodName}\", requestId:{requestId}, noResponse:{(noResponse ? "true" : "false")}, parameterSet:{parameterSet}}}";
            return reqStr;
        }

        private string CreateJsonParamString(List<string> parameters)
        {
            if (parameters.Count == 0) return "true";
            else if (parameters.Count == 1) return parameters[0];
            else
            {
                string result = "{";
                int paramCount = 1;
                foreach (string p in parameters)
                {
                    result += $"Item{paramCount++}:{p},";
                }
                result = result.TrimEnd(',') + '}';
                return result;
            }
        }

        public void Post<M>(in M message)
        {
            if (message is RpcErrorResponse errorResponse)
            {
                Log.ERROR(this, "String-RPC call failed!", errorResponse.ErrorMessage);
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

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink);
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

        private interface IResponseHandler
        {
            bool Handle<M>(in M message);

            TimeFrame LifeTime { get; }

            void Cancel();
        }

        private class ResponseHandler : IResponseHandler
        {
            private readonly long requestId;
            private readonly TaskCompletionSource<string> taskCompletionSource;
            public readonly TimeFrame lifeTime;

            public TimeFrame LifeTime => lifeTime;

            public ResponseHandler(long requestId, TaskCompletionSource<string> taskCompletionSource, TimeSpan timeout)
            {
                this.taskCompletionSource = taskCompletionSource;
                lifeTime = new TimeFrame(timeout);
                this.requestId = requestId;
            }

            public bool Handle<M>(in M message)
            {
                if (message is IRpcResponse myResponse && myResponse.RequestId == this.requestId)
                {
                    taskCompletionSource.SetResult(myResponse.ResultToJson().Trim('"'.ToSingleEntryArray()));
                    return true;
                }
                else return false;
            }

            public void Cancel()
            {
                taskCompletionSource.SetCanceled();
            }
        }

        public void ConnectToAndBack(IReplier replier)
        {
            this.ConnectTo(replier);
            replier.ConnectTo(this);
        }
    }
}