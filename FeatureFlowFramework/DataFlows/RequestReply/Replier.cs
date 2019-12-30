using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public interface IReplier : IDataFlowSource, IDataFlowSink { };

    /// <summary>
    ///     The counterpart of the Requester, receives request messages and sends back reply
    ///     messages. It is thread safe.
    ///     NOTE: The AutoReply feature is experimental!
    /// </summary>
    /// <typeparam name="REQ"> The embedded request type to be received </typeparam>
    /// <typeparam name="REP"> The embedded reply type to be send </typeparam>
    public class Replier<REQ, REP> : IReplier, IReceiver<Request<REQ>>, ISender, IAlternativeDataFlow, IAsyncWaitHandle
    {
        private QueueReceiver<Request<REQ>> receiver = new QueueReceiver<Request<REQ>>();
        private Sender<Reply<REP>> sender = new Sender<Reply<REP>>();
        private CancellationTokenSource cancellationTokenSource;

        public int CountConnectedSinks => sender.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sender.GetConnectedSinks();
        }

        public bool IsEmpty => ((IReceiver<Request<REQ>>)receiver).IsEmpty;

        public bool IsFull => ((IReceiver<Request<REQ>>)receiver).IsFull;

        public int Count => ((IReceiver<Request<REQ>>)receiver).Count;

        public IAsyncWaitHandle WaitHandle => ((IReceiver<Request<REQ>>)receiver).WaitHandle;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            ((IDataFlowSource)sender).ConnectTo(sink);
            return sink;
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sender).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).DisconnectFrom(sink);
        }

        public bool TryPeek(out Request<REQ> nextElement)
        {
            return ((IReceiver<Request<REQ>>)receiver).TryPeek(out nextElement);
        }

        public Request<REQ>[] PeekAll()
        {
            return ((IReceiver<Request<REQ>>)receiver).PeekAll();
        }

        public void Post<M>(in M message)
        {
            ((IDataFlowSink)receiver).Post(message);
        }

        public Task PostAsync<M>(M message)
        {
            return ((IDataFlowSink)receiver).PostAsync(message);
        }

        public Request<REQ>[] ReceiveAll()
        {
            return ((IReceiver<Request<REQ>>)receiver).ReceiveAll();
        }

        public void Send(in IReply message)
        {
            ((ISender)sender).Send(message);
        }

        public void Send<T>(in T message)
        {
            ((ISender)sender).Send(message);
        }

        public Task SendAsync(IReply message)
        {
            return ((ISender)sender).SendAsync(message);
        }

        public Task SendAsync<T>(T message)
        {
            return ((ISender)sender).SendAsync(message);
        }

        public bool IsRequestPending => !receiver.IsEmpty;

        public bool TryReply(Func<REQ, REP> replyAction)
        {
            if(TryReceive(out Request<REQ> request) && request.TryGetMessage(out REQ requestMessage))
            {
                var replyMessage = replyAction(requestMessage);
                Send(request.CreateReply(replyMessage));
                return true;
            }
            return false;
        }

        public async void StartAutoReply(Func<REQ, REP> replyAction)
        {
            try
            {
                StopAutoReply();
                cancellationTokenSource = new CancellationTokenSource();
                var token = cancellationTokenSource.Token;
                while(!token.IsCancellationRequested)
                {
                    await WaitHandle.WaitAsync(token);
                    TryReply(replyAction);
                }
            }
            catch(Exception e)
            {
                Log.ERROR("AutoReply in Replier failed with an exception that was caught!", e.ToString());
            }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }

        public void StopAutoReply()
        {
            if(cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }
        }

        public bool TryReceive(out Request<REQ> message, TimeSpan timeout = default)
        {
            return ((IReceiver<Request<REQ>>)receiver).TryReceive(out message, timeout);
        }

        public Task<AsyncOutResult<bool, Request<REQ>>> TryReceiveAsync(TimeSpan timeout = default)
        {
            return ((IReceiver<Request<REQ>>)receiver).TryReceiveAsync(timeout);
        }

        public Task<bool> WaitAsync()
        {
            return WaitHandle.WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitHandle.WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return WaitHandle.WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitHandle.WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            return WaitHandle.Wait();
        }

        public bool Wait(TimeSpan timeout)
        {
            return WaitHandle.Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            return WaitHandle.Wait(cancellationToken);
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitHandle.Wait(timeout, cancellationToken);
        }

        public bool IsAutoReplyActive => cancellationTokenSource != null;

        public IDataFlowSource Else => ((IAlternativeDataFlow)receiver).Else;

        public Task WaitingTask => WaitHandle.WaitingTask;
    }
}
