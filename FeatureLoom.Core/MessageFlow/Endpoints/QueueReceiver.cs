using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    ///     An endpoint with a queue to receive messages asynchronously and process them in one or
    ///     multiple threads. It is thread-safe. When the maximum queue limit is exceeded, the
    ///     oldest elements are removed until the queue size is back to its limit. Optionally the
    ///     sender has to wait until a timeout exceeds or a consumer has dequeued an element.
    /// </summary>
    /// Uses a normal queue plus locking instead of a concurrent queue because of better performance
    /// in usual scenarios.
    /// <typeparam name="T"> The expected message type </typeparam>
    public sealed class QueueReceiver<T> : IMessageQueue, IReceiver<T>, IAlternativeMessageSource, IAsyncWaitHandle, IMessageSink<T>
    {
        private Queue<T> queue = new Queue<T>();
        private MicroLock queueLock = new MicroLock();
        
        public Type ConsumedMessageType => typeof(T);

        public bool waitOnFullQueue = false;
        public TimeSpan timeoutOnFullQueue;
        public int maxQueueSize = int.MaxValue;
        public bool dropLatestMessageOnFullQueue = true;

        private AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent writerWakeEvent = new AsyncManualResetEvent(true);

        private LazyValue<SourceHelper> alternativeSendingHelper;

        public QueueReceiver(int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default, bool dropLatestMessageOnFullQueue = true)
        {
            this.maxQueueSize = maxQueueSize;
            this.waitOnFullQueue = maxWaitOnFullQueue != default;
            this.timeoutOnFullQueue = maxWaitOnFullQueue;
            this.dropLatestMessageOnFullQueue = dropLatestMessageOnFullQueue;
        }

        public IMessageSource Else => alternativeSendingHelper.Obj;

        public bool IsEmpty => queue.Count == 0;
        public bool IsFull => queue.Count >= maxQueueSize;
        public int Count => queue.Count;
        public IAsyncWaitHandle WaitHandle => readerWakeEvent;

        public void Post<M>(in M message)
        {
            if (message != null && message is T typedMessage)
            {
                if (waitOnFullQueue && IsFull)
                {
                    TimeFrame waitTimeFrame = new TimeFrame(timeoutOnFullQueue);
                    while (!waitTimeFrame.Elapsed())
                    {
                        if (!IsFull)
                        {
                            using (queueLock.Lock())
                            {
                                if (!IsFull)
                                {
                                    EnqueueInLock(typedMessage);
                                    return;
                                }
                            }
                        }
                        writerWakeEvent.Wait(waitTimeFrame.Remaining());
                    }
                }

                if (dropLatestMessageOnFullQueue && IsFull) alternativeSendingHelper.ObjIfExists?.Forward(in message);
                else Enqueue(typedMessage);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (message != null && message is T typedMessage)
            {
                if (waitOnFullQueue && IsFull)
                {
                    TimeFrame waitTimeFrame = new TimeFrame(timeoutOnFullQueue);
                    while (!waitTimeFrame.Elapsed())
                    {
                        if (!IsFull)
                        {
                            using (queueLock.Lock())
                            {
                                if (!IsFull)
                                {
                                    EnqueueInLock(typedMessage);
                                    return;
                                }
                            }
                        }
                        writerWakeEvent.Wait(waitTimeFrame.Remaining());
                    }
                }

                if (dropLatestMessageOnFullQueue && IsFull) alternativeSendingHelper.ObjIfExists?.Forward(in message);
                else Enqueue(typedMessage);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        public async Task PostAsync<M>(M message)
        {
            if (message != null && message is T typedMessage)
            {
                if (waitOnFullQueue && IsFull)
                {
                    TimeSpan waitedTime = TimeSpan.Zero;

                    while (waitedTime < timeoutOnFullQueue)
                    {
                        if (!IsFull)
                        {
                            using (queueLock.Lock())
                            {
                                if (!IsFull)
                                {
                                    EnqueueInLock(typedMessage);
                                    return;
                                }
                            }
                        }
                        writerWakeEvent.Wait(timeoutOnFullQueue);
                    }
                }

                if (waitOnFullQueue && IsFull) await writerWakeEvent.WaitAsync(timeoutOnFullQueue).ConfiguredAwait();
                if (dropLatestMessageOnFullQueue && IsFull) await (alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask);
                else Enqueue(typedMessage);
            }
            else await alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
        }

        private void Enqueue(T message)
        {
            using (queueLock.Lock())
            {
                queue.Enqueue(message);
                EnsureMaxSize();
            }
            if (IsFull) writerWakeEvent.Reset();
            readerWakeEvent.Set();
        }

        // ONLY USE IN LOCKED QUEUE!
        private void EnqueueInLock(T message)
        {            
            queue.Enqueue(message);
            EnsureMaxSize();
            if (IsFull) writerWakeEvent.Reset();
            readerWakeEvent.Set();
        }

        // ONLY USE IN LOCKED QUEUE!
        private void EnsureMaxSize()
        {
            while (queue.Count > maxQueueSize)
            {
                var element = queue.Dequeue();
                alternativeSendingHelper.ObjIfExists?.Forward(element);
            }
        }

        public bool TryReceive(out T message)
        {            
            message = default;
            if (IsEmpty) return false;

            bool success = false;
            using (queueLock.Lock(true))
            {
                success = queue.TryDequeue(out message);
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
            return success;
        }


        public int ReceiveMany(ref T[] items)
        {
            if (IsEmpty) return 0;
            int numElementsReturned = 0;
            using (queueLock.Lock(true))
            {
                if (items.EmptyOrNull()) items = new T[queue.Count];                
                if (queue.Count == items.Length)
                {
                    numElementsReturned = queue.Count;
                    queue.CopyTo(items, 0);
                    queue.Clear();
                }
                else if (queue.Count < items.Length)
                {
                    numElementsReturned = queue.Count;
                    queue.CopyTo(items, 0);
                    Array.Clear(items, queue.Count, items.Length - queue.Count);
                    queue.Clear();
                }
                else
                {
                    numElementsReturned = items.Length;
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = queue.Dequeue();
                    }                    
                }
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
            return numElementsReturned;
        }        

        public int PeekMany(ref T[] items)
        {
            if (IsEmpty) return 0;
            int numElementsReturned = 0;
            using (queueLock.Lock(true))
            {
                if (items.EmptyOrNull()) items = new T[queue.Count];
                if (queue.Count == items.Length)
                {
                    numElementsReturned = queue.Count;
                    queue.CopyTo(items, 0);                    
                }
                else if (queue.Count < items.Length)
                {
                    numElementsReturned = queue.Count;
                    queue.CopyTo(items, 0);
                    Array.Clear(items, queue.Count, items.Length - queue.Count);
                }
                else
                {
                    queue.CopyToArray(items, items.Length);
                    Array.Clear(items, queue.Count, items.Length - queue.Count);
                }
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
            return numElementsReturned;
        }

        public bool TryPeek(out T nextItem)
        {
            nextItem = default;
            if (IsEmpty) return false;

            using (queueLock.Lock(true))
            {
                if (IsEmpty) return false;
                nextItem = queue.Peek();
                return true;
            }
        }


        public void Clear()
        {
            using (queueLock.Lock(true))
            {
                queue.Clear();
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
        }

        public Task WaitingTask => WaitHandle.WaitingTask;

        public object[] GetQueuedMesssages()
        {
            return Array.ConvertAll(queue.ToArray(), input => (object)input);
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

        public bool WouldWait()
        {
            return WaitHandle.WouldWait();
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return WaitHandle.TryConvertToWaitHandle(out waitHandle);
        }

        
    }
}