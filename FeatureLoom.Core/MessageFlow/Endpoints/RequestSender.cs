using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{


    public class RequestSender<REQ, RESP> : IMessageSource<REQ>, IMessageSink<RESP>, IRequester 
        where REQ : IRequestMessage 
        where RESP : IResponseMessage
    {
        TypedSourceValueHelper<REQ> sourceHelper;
        List<ResponseHandler> responseHandlers = new List<ResponseHandler>();
        FeatureLock responseHandlerLock = new FeatureLock();
        TimeSpan timeout = 1.Seconds();
        short senderId = RandomGenerator.Int16();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout">The timeout is not guaranteed and will only be checked if another response is received</param>
        public RequestSender(TimeSpan timeout)
        {
            this.timeout = timeout;
        }

        public RequestSender()
        {
        }

        public Task<RESP> SendRequestAsync(REQ request)
        {
            var handler = ResponseHandler.Create(CreateRequestId());            
            using (responseHandlerLock.Lock())
            {
                responseHandlers.Add(handler);
            }

            request.RequestId = handler.requestId;
            sourceHelper.Forward(request);

            return handler.tcs.Task;
        }

        private void HandleResponse(RESP response)
        {
            if ((short)response.RequestId != senderId) return;

            var now = AppTime.CoarseNow;
            using (responseHandlerLock.Lock())
            {
                for(int i= responseHandlers.Count -1; i >= 0; i--)
                {
                    var handler = responseHandlers[i];
                    bool remove = false;
                    if (handler.requestId == response.RequestId)
                    {
                        handler.tcs.SetResult(response);
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
        }

        public void CleanupTimeouts()
        {
            var now = AppTime.CoarseNow;
            using (responseHandlerLock.Lock())
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

        public Type ConsumedMessageType => throw new NotImplementedException();

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
            if (message is RESP response) HandleResponse(response);
        }

        public void Post<M>(M message)
        {
            if (message is RESP response) HandleResponse(response);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is RESP response) HandleResponse(response);
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

            public static ResponseHandler Create(long requestId) => new ResponseHandler(requestId, AppTime.CoarseNow, new TaskCompletionSource<RESP>());

            public bool Handle(RESP response)
            {
                if (response.RequestId == requestId)
                {
                    tcs.SetResult(response);
                    return true;
                }
                else return false;
            }
        }
    }
}
