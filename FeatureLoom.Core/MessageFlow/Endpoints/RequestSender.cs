using FeatureLoom.Helpers;
using FeatureLoom.Scheduling;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class RequestSender<REQ, RESP> : IMessageSource<IRequestMessage<REQ>>, IMessageSink<IResponseMessage<RESP>>, IRequester 
    {
        TypedSourceValueHelper<IRequestMessage<REQ>> sourceHelper;
        List<ResponseHandler> responseHandlers = new List<ResponseHandler>();
        MicroValueLock responseHandlerLock = new MicroValueLock();
        TimeSpan timeout = 1.Seconds();
        short senderId = RandomGenerator.Int16();
        ActionSchedule schedule = null;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout">The timeout is not guaranteed and will only be checked if another response is received</param>
        public RequestSender(TimeSpan timeout)
        {
            this.timeout = timeout;
            StartTimeoutCheck();
        }

        public RequestSender()
        {
            StartTimeoutCheck();
        }

        private void StartTimeoutCheck()
        {
            schedule = Service<SchedulerService>.Instance.ScheduleAction("RequestSenderTimeout", now => CleanupTimeouts(now), timeout.Multiply(0.5));
        }

        public Task<RESP> SendRequestAsync(REQ message)
        {
            var handler = ResponseHandler.Create(CreateRequestId());
            
            responseHandlerLock.Enter();
            responseHandlers.Add(handler);
            responseHandlerLock.Exit();

            if (message is IRequestMessage<REQ> request) 
            {
                request.RequestId = handler.requestId;
            }
            else 
            {
                request = new RequestMessage<REQ>(message, handler.requestId);                
            }
            
            sourceHelper.Forward(request);
            return handler.tcs.Task;
        }

        public RESP SendRequest(REQ message)
        {
            var handler = ResponseHandler.Create(CreateRequestId());
            
            responseHandlerLock.Enter();
            responseHandlers.Add(handler);
            responseHandlerLock.Exit();

            if (message is IRequestMessage<REQ> request)
            {
                request.RequestId = handler.requestId;
            }
            else
            {
                request = new RequestMessage<REQ>(message, handler.requestId);                
            }

            sourceHelper.Forward(request);
            return handler.tcs.Task.Result;
        }

        private void HandleResponse(RESP response, long requestId)
        {
            var now = AppTime.CoarseNow;

            responseHandlerLock.Enter(true);
            try
            {
                for(int i= responseHandlers.Count -1; i >= 0; i--)
                {
                    var handler = responseHandlers[i];
                    bool remove = false;
                    if (handler.requestId == requestId)
                    {
                        handler.tcs.TrySetResult(response);
                        remove = true;
                    }
                    else if (handler.tcs.Task.IsCanceled)
                    {
                        remove = true;
                    }
                    else if (now > handler.requestTime + timeout)
                    {
                        handler.tcs.TrySetCanceled();
                        remove = true;
                    }

                    if (remove) responseHandlers.RemoveAt(i);
                }
            }
            finally
            {
                responseHandlerLock.Exit();
            }
        }

        public void CleanupTimeouts(DateTime now)
        {
            if (responseHandlers.Count == 0) return;

            responseHandlerLock.Enter();
            try
            {
                for (int i = responseHandlers.Count - 1; i >= 0; i--)
                {
                    var handler = responseHandlers[i];
                    bool remove = false;
                    if (handler.tcs.Task.IsCanceled)
                    {
                        remove = true;
                    }
                    else if (now > handler.requestTime + timeout)
                    {
                        handler.tcs.TrySetCanceled();
                        remove = true;
                    }

                    if (remove) responseHandlers.RemoveAt(i);
                }
            }
            finally
            {
                responseHandlerLock.Exit();
            }
        }

        private long CreateRequestId()
        {
            var id = RandomGenerator.Int64();
            id = id << 16;
            id = id + senderId;
            return id;
        }

        public Type SentMessageType => sourceHelper.SentMessageType;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type ConsumedMessageType => typeof(IResponseMessage<RESP>);

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {

            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            sourceHelper.ConnectTo(replier, weakReference);
            replier.ConnectTo(this, weakReference);
        }

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

        public void Post<M>(in M message)
        {
            PreHandleResponse(message);
        }

        private void PreHandleResponse<M>(M message)
        {
            if (message is IResponseMessage<RESP> responseMessage)
            {
                HandleResponse(responseMessage.Content, responseMessage.RequestId);
            }
        }

        public void Post<M>(M message)
        {
            PreHandleResponse(message);
        }

        public Task PostAsync<M>(M message)
        {
            PreHandleResponse(message);
            return Task.CompletedTask;
        }

        public readonly struct ResponseHandler
        {
            public readonly DateTime requestTime;
            public readonly long requestId;
            public readonly TaskCompletionSource<RESP> tcs;

            public ResponseHandler(long requestId, DateTime requestTime, TaskCompletionSource<RESP> tcs)
            {
                this.requestId = requestId;
                this.requestTime = requestTime;
                this.tcs = tcs;
            }

            public static ResponseHandler Create(long requestId) => new ResponseHandler(requestId, AppTime.CoarseNow, new TaskCompletionSource<RESP>(TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }
}
