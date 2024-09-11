using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class PriorityMessageReceiver<T> : IMessageQueue, IReceiver<T>, IAlternativeMessageSource, IAsyncWaitHandle, IMessageSink<T>
    {
        private AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);
        private MicroLock myLock = new MicroLock();
        private T receivedMessage;
        private LazyValue<SourceHelper> alternativeSendingHelper;
        public IMessageSource Else => alternativeSendingHelper.Obj;
        public bool IsEmpty => !readerWakeEvent.IsSet;
        public bool IsFull => false;
        public int Count => IsEmpty ? 0 : 1;
        public IAsyncWaitHandle WaitHandle => readerWakeEvent;
        private Comparer<T> priorityComparer;
        
        public Type ConsumedMessageType => typeof(T);

        public PriorityMessageReceiver(Comparer<T> priorityComparer)
        {
            this.priorityComparer = priorityComparer;
        }

        public void Post<M>(in M message)
        {
            if (message is T typedMessage)
            {
                if (!readerWakeEvent.IsSet || this.priorityComparer.Compare(receivedMessage, typedMessage) <= 0)
                {
                    using (myLock.Lock())
                    {
                        receivedMessage = typedMessage;
                    }
                    readerWakeEvent.Set();
                }
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (message is T typedMessage)
            {
                if (!readerWakeEvent.IsSet || this.priorityComparer.Compare(receivedMessage, typedMessage) <= 0)
                {
                    using (myLock.Lock())
                    {
                        receivedMessage = typedMessage;
                    }
                    readerWakeEvent.Set();
                }
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage)
            {
                if (!readerWakeEvent.IsSet || this.priorityComparer.Compare(receivedMessage, typedMessage) <= 0)
                {
                    using (myLock.Lock())
                    {
                        receivedMessage = typedMessage;
                    }
                    readerWakeEvent.Set();
                }
            }
            else alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);

            return Task.CompletedTask;
        }

        public bool TryReceive(out T message)
        {            
            message = default;
            if (IsEmpty) return false;

            using (myLock.Lock())
            {
                if (IsEmpty) return false;
                message = receivedMessage;
                receivedMessage = default;
                readerWakeEvent.Reset();
                return true;
            }
        }


        public int ReceiveMany(ref T[] items)
        {
            if (IsEmpty) return 0;
            using (myLock.Lock(true))
            {
                if (IsEmpty) return 0;
                T message = receivedMessage;
                receivedMessage = default;
                if (items.EmptyOrNull()) items = message.ToSingleEntryArray();
                else items[0] = message;
                readerWakeEvent.Reset();
                return 1;
            }
        }

        public int PeekMany(ref T[] items)
        {
            if (IsEmpty) return 0;
            using (myLock.Lock(true))
            {
                if (IsEmpty) return 0;
                T message = receivedMessage;
                if (items.EmptyOrNull()) items = message.ToSingleEntryArray();
                else items[0] = message;
                return 1;
            }
        }

        public bool TryPeek(out T nextItem)
        {
            nextItem = default;
            if (IsEmpty) return false;

            using (myLock.Lock())
            {
                if (IsEmpty) return false;
                nextItem = receivedMessage;
                return true;
            }
        }



        public void Clear()
        {
            using (myLock.Lock())
            {
                readerWakeEvent.Reset();
                receivedMessage = default;
            }
        }

        public object[] GetQueuedMesssages()
        {
            if (IsEmpty) return Array.Empty<object>();

            using (myLock.Lock())
            {
                if (IsEmpty) return Array.Empty<object>();
                T message = receivedMessage;
                return message.ToSingleEntryArray<object>();
            }
        }

        public Task<bool> WaitAsync()
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait();
        }

        public bool Wait(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait(cancellationToken);
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait(timeout, cancellationToken);
        }

        public bool WouldWait()
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WouldWait();
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).TryConvertToWaitHandle(out waitHandle);
        }

        public Task WaitingTask => ((IAsyncWaitHandle)readerWakeEvent).WaitingTask;
    }
}