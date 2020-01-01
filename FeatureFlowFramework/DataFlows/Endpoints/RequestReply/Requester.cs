using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RequestReply
{
    /// <summary>
    ///     Sends a request message and expects back one or multiple reply messages. When sending
    ///     the request a reply-future is produced that can be waited on immediatly for a
    ///     synchrounous execution style or later for an asynchronous execution. The ReplyFuture can
    ///     either expect just one reply message or multiple, then they will be queued and can be
    ///     received one after another. It is usually used in conjunction with one or multiple
    ///     Replier on the other side, but may also be connected to other dataflow elements
    ///     consuming and providing the Request and Reply types. It is thread-safe. Mutliple
    ///     requests can be pending at a time. ReplyFutures are kept with weak references until they
    ///     are garbage collected, so they don't need to be unregistered.
    /// </summary>
    /// <typeparam name="REQ"> The type of the embedded request message </typeparam>
    /// <typeparam name="REP"> The type of the embedded reply message </typeparam>
    public class Requester<REQ, REP> : IRequester
    {
        private DataFlowSourceHelper sender = new DataFlowSourceHelper();
        private List<WeakReference<IReplyFuture<REP>>> replyFutures = new List<WeakReference<IReplyFuture<REP>>>();

        public int CountConnectedSinks => sender.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sender.GetConnectedSinks();
        }

        public IReplyFuture<REP> SendRequest(REQ requestMessage, bool queueMultipleReplies = false)
        {
            long requestId = RandomGenerator.Int64;
            IReplyFuture<REP> replyFuture = queueMultipleReplies ? (IReplyFuture<REP>)new MultiReplyFuture<REP>(requestId) : new SingleReplyFuture<REP>(requestId);
            lock (replyFutures) replyFutures.Add(new WeakReference<IReplyFuture<REP>>(replyFuture));
            var requestWrapper = new Request<REQ>(requestMessage, requestId);
            sender.Forward(requestWrapper);
            return replyFuture;
        }

        public async Task<IReplyFuture<REP>> SendRequestAsync(REQ requestMessage, bool queueMultipleReplies = false)
        {
            long requestId = RandomGenerator.Int64;
            IReplyFuture<REP> replyFuture = queueMultipleReplies ? (IReplyFuture<REP>)new MultiReplyFuture<REP>(requestId) : new SingleReplyFuture<REP>(requestId);
            lock (replyFutures) replyFutures.Add(new WeakReference<IReplyFuture<REP>>(replyFuture));
            var requestWrapper = new Request<REQ>(requestMessage, requestId);
            await sender.ForwardAsync(requestWrapper);
            return replyFuture;
        }

        public bool TryRequestAndReceive(REQ requestMessage, out REP replyMessage, TimeSpan timeout = default)
        {
            return SendRequest(requestMessage).TryReceive(out replyMessage, timeout);
        }

        public async Task<AsyncOutResult<bool, REP>> TryRequestAndReceiveAsync(REQ requestMessage, TimeSpan timeout = default)
        {
            return await SendRequest(requestMessage).TryReceiveAsync(timeout);
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            ((IDataFlowSource)sender).ConnectTo(sink);
            return sink;
        }

        public void ConnectToAndBack(IReplier replier)
        {
            this.ConnectTo(replier as IDataFlowSink);
            replier.ConnectTo(this);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sender).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).DisconnectFrom(sink);
        }

        public void Post<M>(in M message)
        {
            if (!(message is IReply replyWrapper) || !replyWrapper.TryGetMessage<REP>(out REP replyMessage)) return;

            lock (replyFutures)
            {
                for (int i = replyFutures.Count - 1; i >= 0; i--)
                {
                    if (replyFutures[i].TryGetTarget(out IReplyFuture<REP> future) && !future.Terminated)
                    {
                        if (replyWrapper.GetRequestId() == future.RequestId)
                        {
                            future.SetReplyMessage(replyMessage);
                        }
                    }
                    else replyFutures.RemoveAt(i);
                }
            }
        }

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }
    }

    public interface IReplyFuture<REP>
    {
        long RequestId { get; }

        void SetReplyMessage(REP message);

        IAsyncWaitHandle WaitHandle { get; }

        bool TryReceive(out REP replyMessage, TimeSpan timeout = default);

        Task<AsyncOutResult<bool, REP>> TryReceiveAsync(TimeSpan timeout = default);

        bool Terminated { get; set; }
    }

    public class SingleReplyFuture<REP> : IReplyFuture<REP>
    {
        private AsyncManualResetEvent replyReceivedEvent;
        private REP receivedReplyMessage;
        private bool received = false;
        public long RequestId { get; }
        public bool Terminated { get; set; }

        public SingleReplyFuture(long requestId)
        {
            this.RequestId = requestId;
        }

        public void SetReplyMessage(REP message)
        {
            lock (this)
            {
                receivedReplyMessage = message;
                received = true;
                if (replyReceivedEvent != null) replyReceivedEvent.Set();
                Terminated = true;
            }
        }

        public IAsyncWaitHandle WaitHandle
        {
            get
            {
                if (replyReceivedEvent == null) replyReceivedEvent = new AsyncManualResetEvent(false);
                return replyReceivedEvent.AsyncWaitHandle;
            }
        }

        public bool TryReceive(out REP replyMessage, TimeSpan timeout = default)
        {
            bool success = false;
            replyMessage = default;
            if (received ||
                (timeout != default && this.WaitHandle.Wait(timeout)))
                if (received)
                {
                    lock (this)
                    {
                        success = true;
                        replyMessage = this.receivedReplyMessage;
                        this.receivedReplyMessage = default;
                        received = false;
                        if (replyReceivedEvent != null) replyReceivedEvent.Reset();
                    }
                }
            return success;
        }

        public async Task<AsyncOutResult<bool, REP>> TryReceiveAsync(TimeSpan timeout = default)
        {
            bool success = false;
            if (!received) await this.WaitHandle.WaitAsync(timeout, CancellationToken.None);
            success = received;
            received = false;
            return new AsyncOutResult<bool, REP>(success, this.receivedReplyMessage);
        }
    }

    public class MultiReplyFuture<REP> : IReplyFuture<REP>
    {
        private AsyncManualResetEvent replyReceivedEvent;
        private Queue<REP> replyMessageQueue = new Queue<REP>();
        public long RequestId { get; }
        public bool Terminated { get; set; }

        public MultiReplyFuture(long requestId)
        {
            this.RequestId = requestId;
        }

        public void SetReplyMessage(REP message)
        {
            lock (this)
            {
                replyMessageQueue.Enqueue(message);
                if (replyReceivedEvent != null) replyReceivedEvent.Set();
            }
        }

        public IAsyncWaitHandle WaitHandle
        {
            get
            {
                if (replyReceivedEvent == null) replyReceivedEvent = new AsyncManualResetEvent(false);
                return replyReceivedEvent.AsyncWaitHandle;
            }
        }

        public bool TryReceive(out REP replyMessage, TimeSpan timeout = default)
        {
            bool success = false;
            replyMessage = default;
            if (this.replyMessageQueue.Count > 0 ||
                (timeout != default && this.WaitHandle.Wait(timeout)))
                lock (this)
                {
                    if (replyMessageQueue.Count > 0)
                    {
                        replyMessage = this.replyMessageQueue.Dequeue();
                        success = true;
                    }
                    if (replyReceivedEvent != null && this.replyMessageQueue.Count == 0) replyReceivedEvent.Reset();
                }
            return success;
        }

        public async Task<AsyncOutResult<bool, REP>> TryReceiveAsync(TimeSpan timeout = default)
        {
            bool success = false;
            REP replyMessage = default;
            if (replyMessageQueue.Count == 0 && timeout != default) await this.WaitHandle.WaitAsync(timeout, CancellationToken.None);
            lock (this)
            {
                if (replyMessageQueue.Count > 0)
                {
                    replyMessage = this.replyMessageQueue.Dequeue();
                    success = true;
                }
                if (replyReceivedEvent != null && this.replyMessageQueue.Count == 0) replyReceivedEvent.Reset();
            }
            return new AsyncOutResult<bool, REP>(success, replyMessage);
        }
    }
}