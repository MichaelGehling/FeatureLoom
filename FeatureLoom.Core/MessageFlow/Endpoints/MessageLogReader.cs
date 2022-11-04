﻿using FeatureLoom.Collections;
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

        CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        Task executionTask = Task.CompletedTask;
        ForwardingMethod ForwardingMethod { get; set; }


        public MessageLogReader(IReadLogBuffer<T> messageSource, ForwardingMethod forwardingMethod = ForwardingMethod.Synchronous)
        {
            this.messageSource = messageSource;
        }

        public Task ExecutionTask => executionTask;

        public Task Run(CancellationToken ct)
        {
            if (!executionTask.IsCompleted) throw new Exception("MessageLogReader is already running!");
            CancellationToken = ct;
            executionTask = Run();
            return executionTask;
        }



        private async Task Run()
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
                
                await messageSource.WaitForIdAsync(NextMessageId, CancellationToken);
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
