using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Active forwarder that enqueues incoming messages and forwards them on ThreadPool threads.
    /// The number of worker loops scales up (spawns) based on current queue length and scales down (self-terminates)
    /// after an idle timeout when the queue stays empty.
    /// <para>
    /// Forwarding supports three modes (<see cref="ForwardingMethod"/>):
    /// <list type="bullet">
    /// <item><description><see cref="ForwardingMethod.Synchronous"/>: forwards by value.</description></item>
    /// <item><description><see cref="ForwardingMethod.SynchronousByRef"/>: forwards by readonly reference (avoids copying large structs).</description></item>
    /// <item><description><see cref="ForwardingMethod.Asynchronous"/>: forwards via the async path of <see cref="SourceHelper.ForwardAsync{M}(M)"/>.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Order Guarantees:
    /// With a single worker thread (default <c>threadLimit = 1</c>) message order is preserved.
    /// With multiple workers order may change because messages are processed concurrently.
    /// Use a single thread or a different primitive (e.g. <c>AsyncForwarder</c>) if strict ordering is required.
    /// </para>
    /// <para>
    /// Resource Management:
    /// Messages are wrapped in pooled <c>ForwardingMessage&lt;T&gt;</c> instances returned to a <see cref="SharedPool{T}"/>.
    /// Each wrapper must be released exactly once after forwarding. This class enforces that in the worker loop.
    /// </para>
    /// </summary>
    public sealed class QueueForwarder : IMessageFlowConnection
    {
        /// <summary>
        /// Internal abstraction for a pooled forwarding task wrapper.
        /// </summary>
        interface IForwardingMessage
        {
            /// <summary>
            /// Performs the forwarding (may return a task for async forwarding).
            /// Returning <c>null</c> indicates synchronous completion.
            /// </summary>
            Task Forward(SourceHelper target);

            /// <summary>
            /// MUST be called exactly once after processing to return the wrapper to the pool.
            /// </summary>
            void Release();
        }

        /// <summary>
        /// Generic pooled forwarding wrapper. Instances are provided by <see cref="SharedPool{T}"/>.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        class ForwardingMessage<T> : IForwardingMessage
        {
            public T message;
            public ForwardingMethod forwardingMethod;

            public Task Forward(SourceHelper target)
            {
                if (forwardingMethod == ForwardingMethod.Synchronous) target.Forward(message);
                else if (forwardingMethod == ForwardingMethod.SynchronousByRef) target.Forward(in message);
                else if (forwardingMethod == ForwardingMethod.Asynchronous) return target.ForwardAsync(message);
                return null;
            }

            public void Release()
            {
                SharedPool<ForwardingMessage<T>>.Return(this);
            }
        }

        private readonly SourceHelper sourceHelper = new SourceHelper();
        private readonly IReceiver<IForwardingMessage> receiver;

        private MicroLock threadManageLock = new MicroLock();

        /// <summary>
        /// Maximum number of concurrent worker loops allowed (dynamic scaling will not exceed this).
        /// </summary>
        private readonly int threadLimit;

        /// <summary>
        /// Scaling trigger factor. A new worker is spawned when <c>receiver.Count &gt;= numThreads * spawnThreshold</c>.
        /// Minimum value is 1.
        /// </summary>
        private readonly int spawnThreshold;

        /// <summary>
        /// Maximum idle time (in milliseconds) a worker loop waits for new messages before terminating.
        /// </summary>
        private readonly int maxIdleMilliseconds;

        private int numThreads = 0;
        private int maxThreadsOccurred = 0;

        /// <summary>
        /// Highest number of concurrent worker threads that have ever been active simultaneously.
        /// Useful for tuning <see cref="threadLimit"/> and <see cref="spawnThreshold"/>.
        /// </summary>
        public int MaxThreadsOccurred => maxThreadsOccurred;

        /// <summary>
        /// Current number of active worker threads (forwarding loops).
        /// </summary>
        public int CountThreads => numThreads;

        /// <summary>
        /// True when there are no connected sinks and forwarding is effectively disabled.
        /// Posting in that state returns immediately without enqueuing.
        /// </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        /// <summary>
        /// Creates a new queue-based forwarder with dynamic thread scaling.
        /// </summary>
        /// <param name="threadLimit">Maximum number of concurrent worker threads; clamped to at least 1.</param>
        /// <param name="maxIdleMilliseconds">Maximum idle time a worker thread waits before terminating itself.</param>
        /// <param name="spawnThresholdFactor">Spawn threshold factor (see <see cref="spawnThreshold"/>).</param>
        /// <param name="maxQueueSize">Maximum number of queued messages; when full, behavior depends on <paramref name="dropLatestMessageOnFullQueue"/>.</param>
        /// <param name="maxWaitOnFullQueue">Maximum time a sender waits when queue is full (blocking semantics defined by receiver implementation).</param>
        /// <param name="dropLatestMessageOnFullQueue">If true drops the newly posted message when full; otherwise drops the oldest.</param>
        public QueueForwarder(
            int threadLimit = 1,
            int maxIdleMilliseconds = 50,
            int spawnThresholdFactor = 50,
            int maxQueueSize = int.MaxValue,
            TimeSpan maxWaitOnFullQueue = default,
            bool dropLatestMessageOnFullQueue = true)
        {
            receiver = new QueueReceiver<IForwardingMessage>(maxQueueSize, maxWaitOnFullQueue, dropLatestMessageOnFullQueue);
            this.threadLimit = threadLimit;
            spawnThreshold = spawnThresholdFactor;
            this.maxIdleMilliseconds = maxIdleMilliseconds;

            if (spawnThreshold < 1) spawnThreshold = 1;
            if (this.threadLimit < 1) this.threadLimit = 1;
        }

        /// <summary>
        /// Number of currently connected sinks (excludes collected weak references).
        /// </summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary>
        /// Posts a message by readonly reference (see <see cref="ForwardingMethod.SynchronousByRef"/>).
        /// Enqueues the message for forwarding. Returns immediately.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">Message instance.</param>
        public void Post<M>(in M message)
        {
            if (sourceHelper.NoConnectedSinks) return;
            IForwardingMessage forwardingMessage = PrepareForwardingMessage(in message, ForwardingMethod.SynchronousByRef);
            receiver.Post(in forwardingMessage);
            ManageThreadCount();
        }

        /// <summary>
        /// Posts a message by value (see <see cref="ForwardingMethod.Synchronous"/>). Enqueues the message for forwarding.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">Message instance.</param>
        public void Post<M>(M message)
        {
            if (sourceHelper.NoConnectedSinks) return;
            IForwardingMessage forwardingMessage = PrepareForwardingMessage(message, ForwardingMethod.Synchronous);
            receiver.Post(in forwardingMessage);
            ManageThreadCount();
        }

        /// <summary>
        /// Posts a message for asynchronous forwarding (see <see cref="ForwardingMethod.Asynchronous"/>).
        /// Returns a completed task; actual forwarding occurs on worker loop threads.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">Message instance.</param>
        /// <returns>A completed task (does not represent forwarding completion).</returns>
        public Task PostAsync<M>(M message)
        {
            if (sourceHelper.NoConnectedSinks) return Task.CompletedTask;
            IForwardingMessage forwardingMessage = PrepareForwardingMessage(in message, ForwardingMethod.Asynchronous);
            receiver.Post(in forwardingMessage);
            ManageThreadCount();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates or takes a pooled wrapper for the message and sets its forwarding method.
        /// </summary>
        private static IForwardingMessage PrepareForwardingMessage<M>(in M message, ForwardingMethod forwardingMethod)
        {
            if (!SharedPool<ForwardingMessage<M>>.IsInitialized)
            {
                SharedPool<ForwardingMessage<M>>.TryInit(
                    () => new ForwardingMessage<M>(),
                    item =>
                    {
                        item.message = default;
                        item.forwardingMethod = default;
                    });
            }

            var forwardingMessage = SharedPool<ForwardingMessage<M>>.Take();
            forwardingMessage.message = message;
            forwardingMessage.forwardingMethod = forwardingMethod;
            return forwardingMessage;
        }

        /// <summary>
        /// Spawns a new worker loop if scaling criteria are met.
        /// </summary>
        private void ManageThreadCount()
        {
            if (numThreads * spawnThreshold >= receiver.Count || numThreads >= threadLimit) return;
            
            using (threadManageLock.Lock())
            {
                if (numThreads * spawnThreshold >= receiver.Count || numThreads >= threadLimit) return;

                Interlocked.Increment(ref numThreads);
                if (numThreads > maxThreadsOccurred) maxThreadsOccurred = numThreads;
                Task.Run(ForwardingLoop);
            }
            
        }

        /// <summary>
        /// Worker loop: receives queued messages until idle timeout elapses.
        /// Each received wrapper is forwarded (awaiting async tasks if necessary) and then released to the pool.
        /// </summary>
        private async Task ForwardingLoop()
        {
            var timeout = maxIdleMilliseconds.Milliseconds();
            while ((await receiver.TryReceiveAsync(timeout).ConfiguredAwait()).TryOut(out IForwardingMessage forwardingMessage))
            {
                try
                {
                    Task task = forwardingMessage.Forward(sourceHelper);
                    if (task != null) await task.ConfiguredAwait();
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build("Exception caught in QueueForwarder while sending.", e);
                }
                finally
                {
                    forwardingMessage.Release();
                }
            }

            Interlocked.Decrement(ref numThreads);
        }

        /// <summary>
        /// Connects this forwarder to a sink. If <paramref name="weakReference"/> is true, a weak reference is used.
        /// </summary>
        public void ConnectTo(IMessageSink sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

        /// <summary>
        /// Connects this forwarder to a bidirectional flow element and returns it typed as a source for chaining.
        /// </summary>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

        /// <summary>
        /// Disconnects a specific sink if it is currently connected.
        /// </summary>
        public void DisconnectFrom(IMessageSink sink) => sourceHelper.DisconnectFrom(sink);

        /// <summary>
        /// Disconnects all currently connected sinks.
        /// </summary>
        public void DisconnectAll() => sourceHelper.DisconnectAll();

        /// <summary>
        /// Returns a snapshot of currently connected sinks (invalid weak references pruned lazily).
        /// </summary>
        public IMessageSink[] GetConnectedSinks() => sourceHelper.GetConnectedSinks();

        /// <summary>
        /// Checks whether the specified sink is currently connected.
        /// </summary>
        public bool IsConnected(IMessageSink sink) => sourceHelper.IsConnected(sink);
    }
}