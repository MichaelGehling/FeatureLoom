using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
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
    public partial class StringRpcCaller : IMessageSource, IMessageSink, IRequester
    {
        private SourceValueHelper sourceHelper;
        private List<IResponseHandler> responseHandlers = new List<IResponseHandler>();
        private MicroLock responseHandlersLock = new MicroLock();
        private readonly TimeSpan timeout;
        private readonly ISchedule timeoutSchedule;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        public StringRpcCaller(TimeSpan timeout)
        {
            this.timeout = timeout;
            timeoutSchedule = ((Action<DateTime>)CheckForTimeouts).ScheduleForRecurringExecution("StringRpcCaller_Timeout", timeout.Multiply(0.5));
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

        public void CallMultiResponse(string methodCall, IMessageSink sink)
        {
            var requestId = RandomGenerator.Int64();
            string serializedRpcRequest = BuildJsonRpcRequest(methodCall, requestId, false);
            using (responseHandlersLock.Lock(true))
            {
                responseHandlers.Add(new MultiResponseHandler(requestId, sink, timeout));
            }
            sourceHelper.Forward(serializedRpcRequest);
        }

        public Task<string> CallAsync(string methodCall)
        {
            var requestId = RandomGenerator.Int64();
            string serializedRpcRequest = BuildJsonRpcRequest(methodCall, requestId, false);
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (responseHandlersLock.Lock(true))
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
            string reqStr = $"{{\"method\":\"{methodName}\", \"requestId\":{requestId.ToString()}, \"noResponse\":{(noResponse ? "true" : "false")}, \"parameterSet\":{parameterSet}}}";
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
                    result += $"\"Item{paramCount++}\":{p},";
                }
                result = result.TrimEnd(',') + '}';
                return result;
            }
        }

        public void Post<M>(in M message)
        {
            if (message is RpcErrorResponse errorResponse)
            {
                OptLog.ERROR()?.Build("String-RPC call failed!", errorResponse.ErrorMessage);
            }
            else if (message is IRpcResponse)
            {
                DateTime now = AppTime.Now;

                using (responseHandlersLock.Lock(true))
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
                OptLog.ERROR()?.Build("String-RPC call failed!", errorResponse.ErrorMessage);
            }
            else if (message is IRpcResponse)
            {
                DateTime now = AppTime.Now;

                using (responseHandlersLock.Lock(true))
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

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
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

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}