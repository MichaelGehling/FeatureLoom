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
    ///     Optionally, a priority queue can be used to sort the incoming messages based on an individual comparer.
    ///     If the order and exact number of used threads doesn't matter, consider to use the AsyncForwarder, as it can be more efficient in such scenario.
    ///     Note: Using more than one thread may alter the order of forwarded messages!
    ///     Note: When used for struct messages they will be boxed as an object. If you only have a single struct message type, use the typed QueueForwarder<T> instead to avoid boxing
    /// </summary>
    public class QueueForwarder : QueueForwarder<object>
    {
        public QueueForwarder(int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default, bool dropLatestMessageOnFullQueue = true)
            : base(threadLimit, maxIdleMilliseconds, spawnThresholdFactor, maxQueueSize, maxWaitOnFullQueue, dropLatestMessageOnFullQueue)
        {
        }

        public QueueForwarder(Comparer<object> priorityComparer, int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default) 
            : base(priorityComparer, threadLimit, maxIdleMilliseconds, spawnThresholdFactor, maxQueueSize, maxWaitOnFullQueue)
        {
        }
    }

    /// <summary>
    ///     Creates an active forwarder that queues incoming messages and forwards them in threads
    ///     from the ThreadPool. The number of threads is scaled dynamically based on load. The
    ///     scaling parameters can be configured.
    ///     If the order and exact number of used threads doesn't matter, consider to use the AsyncForwarder, as it can be more efficient in such scenario.
    ///     Optionally, a priority queue can be used to sort the incoming messages based on an individual comparer.
    ///     Note: Using more than one thread may alter the order of forwarded messages!
    /// </summary>
    public class QueueForwarder<T> : IMessageFlowConnection<T>
    {
        protected TypedSourceValueHelper<T> sourceHelper;
        protected IReceiver<T> receiver;
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
            this.receiver = new QueueReceiver<T>(maxQueueSize, maxWaitOnFullQueue, dropLatestMessageOnFullQueue);
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
        public QueueForwarder(Comparer<T> priorityComparer, int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default)
        {
            this.receiver = new PriorityQueueReceiver<T>(priorityComparer, maxQueueSize, maxWaitOnFullQueue);
            this.threadLimit = threadLimit;
            this.spawnThreshold = spawnThresholdFactor;
            this.maxIdleMilliseconds = maxIdleMilliseconds;

            if (this.spawnThreshold < 1) this.spawnThreshold = 1;
            if (this.threadLimit < 1) this.threadLimit = 1;
        }

        public int CountConnectedSinks => receiver.Count;        
       
        public void Post<M>(in M message)
        {
            if (sourceHelper.CountConnectedSinks > 0)
            {
                receiver.Post(in message);
                ManageThreadCount();
            }
        }

        public void Post<M>(M message)
        {
            Post(in message);
        }

        private void ManageThreadCount()
        {
            if (numThreads * spawnThreshold < receiver.Count && numThreads < threadLimit)
            {
                Interlocked.Increment(ref numThreads);
                if (numThreads > maxThreadsOccurred) maxThreadsOccurred = numThreads;
                _ = Run();
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (sourceHelper.CountConnectedSinks > 0)
            {
                Task task = receiver.PostAsync(message);
                ManageThreadCount();
                return task;
            }
            else return Task.CompletedTask;
        }

        private async Task Run()
        {
            while ((await receiver.TryReceiveAsync(maxIdleMilliseconds.Milliseconds())).Out(out T message))
            {
                try
                {
                    sourceHelper.Forward(message);
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