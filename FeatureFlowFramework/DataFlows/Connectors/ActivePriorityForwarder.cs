using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     Creates an active forwarder that queues incoming messages and forwards them in threads
    ///     from the ThreadPool. The number of threads is scaled dynamically based on load. The
    ///     scaling parameters can be configured. The messages in the queue are ordered by priority,
    ///     so that the messages with highest priority are forwarded first. The priority is defined
    ///     by priority comparer to be provided in the constructor. Message types not supported by
    ///     the comparer are ignored.
    ///     Note: Using more than one thread may alter the order of forwarded messages!
    /// </summary>
    /// <typeparam name="T"> The supported message type </typeparam>
    public class ActivePriorityForwarder<T> : Forwarder
    {
        private PriorityQueueReceiver<T> receiver;
        public volatile int threadLimit;
        public volatile int spawnThreshold;
        public volatile int maxIdleMilliseconds;

        private volatile int numThreads = 0;

        public int CountThreads => numThreads;

        /// <summary>
        ///     Creates an active forwarder that queues incoming messages and forwards them in
        ///     threads from the ThreadPool. The number of threads is scaled dynamically based on
        ///     load. The scaling parameters can be configured. The messages in the queue are
        ///     ordered by priority, so that the messages with highest priority are forwarded first.
        ///     The priority is defined by priority comparer to be provided in the constructor.
        ///     Message types not supported by the comparer are ignored.
        ///     Note: Using more than one thread may alter the order of forwarded messages!
        /// </summary>
        /// <param name="priorityComparer"> comparer to sort incoming messages by priority </param>
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
        /// <param name="maxQueueSize"> muximum number of messages in the queue </param>
        /// <param name="maxWaitOnFullQueue"> how long a sender may wait on a full queue </param>
        public ActivePriorityForwarder(Comparer<T> priorityComparer, int threadLimit = 1, int maxIdleMilliseconds = 50, int spawnThresholdFactor = 10, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default)
        {
            receiver = new PriorityQueueReceiver<T>(priorityComparer, maxQueueSize, maxWaitOnFullQueue);

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

        public override Task PostAsync<M>(M message)
        {
            Task task = receiver.PostAsync(message);
            ManageThreadCount();
            return task;
        }

        private void ManageThreadCount()
        {
            if(numThreads * spawnThreshold < receiver.CountQueuedMessages && numThreads < threadLimit)
            {
                lock (receiver) { numThreads++; }
                new Task(Run).Start();
            }
        }

        private void Run()
        {
            while(receiver.TryReceive(out T message, maxIdleMilliseconds.Milliseconds()))
            {
                try
                {
                    base.Post(message);
                }
                catch(Exception e)
                {
                    Log.ERROR("Exception caught in ActivePriorityForwarder while sending.", e.ToString());
                }
            }
            lock (receiver) { numThreads--; }
        }
    }
}
