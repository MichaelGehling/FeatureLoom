using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Services.MetaData;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     Creates an active forwarder that queues incoming messages and forwards them in threads
    ///     from the ThreadPool. The number of threads is scaled dynamically based on load. The
    ///     scaling parameters can be configured.
    ///     Note: Using more than one thread may alter the order of forwarded messages!
    /// </summary>
    public class ActiveForwarder : ActiveForwarder<object>
    {
        public ActiveForwarder(int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default, bool dropLatestMessageOnFullQueue = true)
            : base(threadLimit, maxIdleMilliseconds, spawnThresholdFactor, maxQueueSize, maxWaitOnFullQueue, dropLatestMessageOnFullQueue)
        {
        }
    }

    /// <summary>
    ///     Creates an active forwarder that queues incoming messages and forwards them in threads
    ///     from the ThreadPool. The number of threads is scaled dynamically based on load. The
    ///     scaling parameters can be configured.
    ///     Note: Using more than one thread may alter the order of forwarded messages!
    /// </summary>
    public class ActiveForwarder<T> : Forwarder, IDataFlowConnection<T>
    {
        private readonly QueueReceiver<T> receiver;
        FeatureLock receiverLock = new FeatureLock();
        public volatile int threadLimit;
        public volatile int spawnThreshold;
        public volatile int maxIdleMilliseconds;

        private volatile int numThreads = 0;

        public int CountThreads => numThreads;

        /// <summary>
        ///     Creates an active forwarder that queues incoming messages and forwards them in
        ///     threads from the ThreadPool. The number of threads is scaled dynamically based on
        ///     load. The scaling parameters can be configured.
        ///     Note: Using more than one thread may alter the order of forwarded messages!
        /// </summary>
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
        /// <param name="dropLatestMessageOnFullQueue">
        ///     if true, the newest message is dropped when the queue is full, if false the oldest one
        /// </param>
        public ActiveForwarder(int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default, bool dropLatestMessageOnFullQueue = true)
        {
            this.receiver = new QueueReceiver<T>(maxQueueSize, maxWaitOnFullQueue, dropLatestMessageOnFullQueue);
            this.threadLimit = threadLimit;
            this.spawnThreshold = spawnThresholdFactor;
            this.maxIdleMilliseconds = maxIdleMilliseconds;

            if(this.spawnThreshold < 1) this.spawnThreshold = 1;
            if(this.threadLimit < 1) this.threadLimit = 1;
        }

        public int Count => receiver.Count;

        public override void Post<M>(in M message)
        {
            receiver.Post(message);
            ManageThreadCount();
        }

        private void ManageThreadCount()
        {
            if(numThreads * spawnThreshold < receiver.CountQueuedMessages && numThreads < threadLimit)
            {
                using(receiverLock.Lock())
                {
                    numThreads++;
                }
                new Task(Run).Start();
            }
        }

        public override Task PostAsync<M>(M message)
        {
            Task task = receiver.PostAsync(message);
            ManageThreadCount();
            return task;
        }

        private void Run()
        {
            while(receiver.TryReceiveAsync(maxIdleMilliseconds.Milliseconds()).Result.Out(out T message))
            {
                try
                {
                    base.Post(message);
                }
                catch(Exception e)
                {
                    Log.ERROR(this.GetHandle(), "Exception caught in ActiveForwarder while sending.", e.ToString());
                }
            }
            using (receiverLock.Lock())
            {
                numThreads--;
            }
        }
    }
}