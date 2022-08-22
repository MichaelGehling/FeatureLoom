using FeatureLoom.Collections;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public class MessageLogReader<T> : IMessageSource<T>
    {
        TypedSourceHelper<T> sourceHelper = new TypedSourceHelper<T>();

        IReadLogBuffer<T> messageSource;
        long NextMessageId { get; set; } = 0;

        CancellationToken CancellationToken { get; set; }        
        Task executionTask = Task.CompletedTask;
        ForwardingMethod ForwardingMethod { get; set; } = ForwardingMethod.Asynchronous;


        public MessageLogReader(IReadLogBuffer<T> messageSource, CancellationToken ct)
        {
            this.messageSource = messageSource;
            CancellationToken = ct;
            if (ct.IsCancellationRequested) return;
            executionTask = Task.Run(Run);
        }

        public Task ExecutionTask => executionTask;

        private Task Run(CancellationToken ct)
        {
            if (executionTask.IsCompleted || CancellationToken.IsCancellationRequested)
            {
                CancellationToken = ct;
                executionTask = Task.Run(Run);
            }
            else CancellationToken = ct;

            return executionTask;
        }


        private async void Run()
        {
            ObjectHandle handle = this.GetHandle();
            while(!CancellationToken.IsCancellationRequested)
            {
                while (!CancellationToken.IsCancellationRequested && NextMessageId < messageSource.OldestAvailableId)
                {
                    var oldest = messageSource.OldestAvailableId;
                    Log.WARNING(handle, $"Missed messages with log Ids {NextMessageId} - {oldest - 1}");
                    NextMessageId = oldest;
                }

                while(!CancellationToken.IsCancellationRequested && messageSource.TryGetFromId(NextMessageId, out T message))
                {
                    if (ForwardingMethod == ForwardingMethod.Synchronous) sourceHelper.Forward(message);
                    if (ForwardingMethod == ForwardingMethod.SynchronousByRef) sourceHelper.Forward(in message);
                    if (ForwardingMethod == ForwardingMethod.Asynchronous) await sourceHelper.ForwardAsync(message);
                }

                // Timeout to handle the rare case that latestId changes between check and waiting
                while (!CancellationToken.IsCancellationRequested && NextMessageId > messageSource.LatestId) await messageSource.WaitHandle.WaitAsync(1.Seconds(), CancellationToken);
            }
        }

        public Type SentMessageType => ((ITypedMessageSource)sourceHelper).SentMessageType;

        public int CountConnectedSinks => ((IMessageSource)sourceHelper).CountConnectedSinks;

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            ((IMessageSource)sourceHelper).ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return ((IMessageSource)sourceHelper).ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            ((IMessageSource)sourceHelper).DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            ((IMessageSource)sourceHelper).DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return ((IMessageSource)sourceHelper).GetConnectedSinks();
        }
    }
}
