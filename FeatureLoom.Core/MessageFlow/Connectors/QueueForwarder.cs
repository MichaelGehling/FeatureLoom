using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    ///     Creates an active forwarder that queues incoming messages and forwards them in threads
    ///     from the ThreadPool. The number of threads is scaled dynamically based on load. The
    ///     scaling parameters can be configured.
    ///     If the order and exact number of used threads doesn't matter, consider to use the AsyncForwarder, as it can be more efficient in such scenario.
    ///     Optionally, a priority queue can be used to sort the incoming messages based on an individual comparer.
    ///     Note: Using more than one thread may alter the order of forwarded messages!
    /// </summary>
    public sealed class QueueForwarder<T> : IMessageFlowConnection<T>
    {
        readonly struct ForwardingMessage
        {
            public readonly T message;
            public readonly ForwardingMethod forwardingMethod;

            public ForwardingMessage(T message, ForwardingMethod forwardingMethod)
            {
                this.message = message;
                this.forwardingMethod = forwardingMethod;
            }
        }

        private TypedSourceValueHelper<T> sourceHelper;
        private IReceiver<ForwardingMessage> receiver;
        public volatile int threadLimit;
        public volatile int spawnThreshold;
        public volatile int maxIdleMilliseconds;

        private volatile int numThreads = 0;
        private volatile int maxThreadsOccurred = 0;

        public int MaxThreadsOccurred => maxThreadsOccurred;
        public int CountThreads => numThreads;
        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

        /// <summary>
        ///     Creates an active forwarder that queues incoming messages and forwards them in
        ///     threads from the ThreadPool. The number of threads is scaled dynamically based on
        ///     load. The scaling parameters can be configured.
        ///     Note: Using more than one thread may alter the order of forwarded messages!
        /// </summary>
        /// <param name="threadLimit">
        ///     the maximum number of parallel threads that are fetching messages from the queue and
        ///     forwarding them.
        /// </param>
        /// <param name="maxIdleMilliseconds">
        ///     the maximum time a thread stays idle (when the wueue is empty) before it terminates
        /// </param>
        /// <param name="spawnThresholdFactor">
        ///     a new thread is spawned if the number of queued items exceeds the current number of threads*spawnThresholdFactor
        /// </param>
        /// <param name="maxQueueSize"> the maximum number of messages in the queue </param>
        /// <param name="maxWaitOnFullQueue">
        ///     the maximum time a sender waits when the queue is full
        /// </param>
        /// <param name="dropLatestMessageOnFullQueue">
        ///     if true, the newest message is dropped when the queue is full, if false the oldest one
        /// </param>
        public QueueForwarder(int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default, bool dropLatestMessageOnFullQueue = true)
        {
            this.receiver = new QueueReceiver<ForwardingMessage>(maxQueueSize, maxWaitOnFullQueue, dropLatestMessageOnFullQueue);
            this.threadLimit = threadLimit;
            this.spawnThreshold = spawnThresholdFactor;
            this.maxIdleMilliseconds = maxIdleMilliseconds;

            if (this.spawnThreshold < 1) this.spawnThreshold = 1;
            if (this.threadLimit < 1) this.threadLimit = 1;
        }

        /// <summary>
        ///     Creates an active forwarder that queues incoming messages and forwards them in
        ///     threads from the ThreadPool. The number of threads is scaled dynamically based on
        ///     load. The scaling parameters can be configured.
        ///     The incoming messages are ordered by priority before they are forwarded.
        ///     Note: Using more than one thread may alter the order of forwarded messages!
        /// </summary>
        /// <param name="priorityComparer"> Used to compare the priority of incoming messages</param>
        /// <param name="threadLimit">
        ///     the maximum number of parallel threads that are fetching messages from the queue and
        ///     forwarding them
        /// </param>
        /// <param name="maxIdleMilliseconds">
        ///     the maximum time a thread stays idle (when the wueue is empty) before it terminates
        /// </param>
        /// <param name="spawnThresholdFactor">
        ///     a new thread is spawned if the number of queued items exceeds the current number of threads*spawnThresholdFactor
        /// </param>
        /// <param name="maxQueueSize"> the maximum number of messages in the queue </param>
        /// <param name="maxWaitOnFullQueue">
        ///     the maximum time a sender waits when the queue is full
        /// </param>
        public QueueForwarder(IComparer<T> priorityComparer, int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default)
        {
            Comparer<ForwardingMessage> forwardingMessageComparer = Comparer<ForwardingMessage>.Create((x, y) => priorityComparer.Compare(x.message, y.message));
            this.receiver = new PriorityQueueReceiver<ForwardingMessage>(forwardingMessageComparer, maxQueueSize, maxWaitOnFullQueue);
            this.threadLimit = threadLimit;
            this.spawnThreshold = spawnThresholdFactor;
            this.maxIdleMilliseconds = maxIdleMilliseconds;

            if (this.spawnThreshold < 1) this.spawnThreshold = 1;
            if (this.threadLimit < 1) this.threadLimit = 1;
        }

        public int CountConnectedSinks => receiver.Count;        
       
        public void Post<M>(in M message)
        {
            if (message is T typedMessage && sourceHelper.CountConnectedSinks > 0)
            {
                ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.SynchronousByRef);
                receiver.Post(in forwardingMessage);
                ManageThreadCount();
            }
        }

        public void Post<M>(M message)
        {
            if (message is T typedMessage && sourceHelper.CountConnectedSinks > 0)
            {
                ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.Synchronous);
                receiver.Post(forwardingMessage);
                ManageThreadCount();
            }
        }

        private void ManageThreadCount()
        {
            if (numThreads * spawnThreshold < receiver.Count && numThreads < threadLimit)
            {
                Interlocked.Increment(ref numThreads);
                if (numThreads > maxThreadsOccurred) maxThreadsOccurred = numThreads;
                Task.Run(ForwardingLoop);
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage && sourceHelper.CountConnectedSinks > 0)
            {
                ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.Asynchronous);
                receiver.Post(forwardingMessage);
                ManageThreadCount();
            }
            return Task.CompletedTask;
        }

        private async Task ForwardingLoop()
        {
            var timeout = maxIdleMilliseconds.Milliseconds();
            while ((await receiver.TryReceiveAsync(timeout).ConfigureAwait(false)).TryOut(out ForwardingMessage forwardingMessage))
            {
                try
                {
                    if (forwardingMessage.forwardingMethod == ForwardingMethod.Synchronous) sourceHelper.Forward(forwardingMessage.message);
                    else if (forwardingMessage.forwardingMethod == ForwardingMethod.SynchronousByRef) sourceHelper.Forward(in forwardingMessage.message);
                    else if (forwardingMessage.forwardingMethod == ForwardingMethod.Asynchronous) await sourceHelper.ForwardAsync(forwardingMessage.message).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log.ERROR(this.GetHandle(), "Exception caught in ActiveForwarder while sending.", e.ToString());
                }
            }

            Interlocked.Decrement(ref numThreads);
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
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
    }
}