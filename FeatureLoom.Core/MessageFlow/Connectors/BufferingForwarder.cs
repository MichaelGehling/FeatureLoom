using FeatureLoom.Collections;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Forwards messages while buffering the most recent messages to replay them to newly connecting sinks.
    /// This non-generic convenience type buffers messages of type <see cref="object"/>.
    /// </summary>
    public class BufferingForwarder : BufferingForwarder<object>
    {
        /// <summary>
        /// Creates a new <see cref="BufferingForwarder"/> with a fixed-size buffer.
        /// </summary>
        /// <param name="bufferSize">Maximum number of messages to retain for replay. Must be greater than 0.</param>
        public BufferingForwarder(int bufferSize) : base(bufferSize)
        {
        }
    }

    /// <summary>
    /// Forwards messages and keeps a circular buffer of the last <c>N</c> messages of type <typeparamref name="T"/>.
    /// When a new sink connects, the buffered messages are synchronously replayed in order before live delivery starts.
    /// </summary>
    /// <typeparam name="T">The message type that will be buffered for replay.</typeparam>
    /// <remarks>
    /// - Messages that are not assignable to <typeparamref name="T"/> are forwarded but not buffered.<br/>
    /// - Thread-safe: concurrent posting and connecting is supported. A read/write lock protects buffer access and connection replays.
    /// </remarks>
    public class BufferingForwarder<T> : IMessageFlowConnection<T>
    {
        private SourceValueHelper sourceHelper;
        private readonly CircularLogBuffer<T> buffer;
        private readonly FeatureLock bufferLock = new FeatureLock();

        /// <summary>
        /// Gets the type of messages sent downstream (always <typeparamref name="T"/>).
        /// </summary>
        public Type SentMessageType => typeof(T);

        /// <summary>
        /// Gets the type of messages consumed by this forwarder (always <typeparamref name="T"/>).
        /// Note: Other message types are still forwarded but not buffered.
        /// </summary>
        public Type ConsumedMessageType => typeof(T);

        /// <summary>
        /// Creates a new <see cref="BufferingForwarder{T}"/> with a fixed-size circular buffer.
        /// </summary>
        /// <param name="bufferSize">Maximum number of messages to retain for replay. Must be greater than 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bufferSize"/> is not greater than 0.</exception>
        public BufferingForwarder(int bufferSize)
        {
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be > 0.");
            buffer = new CircularLogBuffer<T>(bufferSize, false);
        }

        /// <summary>
        /// Replays the current buffer to the given sink in FIFO order.
        /// Must be called while holding at least a read-lock on <see cref="bufferLock"/>.
        /// </summary>
        /// <param name="sink">The sink to receive the buffered messages.</param>
        private void OnConnection(IMessageSink sink)
        {
            var bufferedMessages = buffer.GetAllAvailable(0, out _, out _);
            foreach (var msg in bufferedMessages)
            {
                sink.Post(msg);
            }
        }

        /// <summary>
        /// Gets the number of currently connected sinks (alive references).
        /// </summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NotConnected;

        /// <summary>
        /// Disconnects all currently connected sinks.
        /// </summary>
        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        /// <summary>
        /// Disconnects a specific sink.
        /// </summary>
        /// <param name="sink">The sink to disconnect.</param>
        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        /// <summary>
        /// Returns a snapshot of all currently connected sinks.
        /// </summary>
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <summary>
        /// Forwards a message by reference and, if it is of type <typeparamref name="T"/>, adds it to the buffer first.
        /// </summary>
        /// <typeparam name="M">The compile-time message type.</typeparam>
        /// <param name="message">The message to forward.</param>
        public void Post<M>(in M message)
        {
            if (message is T msgT) using (bufferLock.Lock()) buffer.Add(msgT);
            sourceHelper.Forward(in message);
        }

        /// <summary>
        /// Forwards a message and, if it is of type <typeparamref name="T"/>, adds it to the buffer first.
        /// </summary>
        /// <typeparam name="M">The compile-time message type.</typeparam>
        /// <param name="message">The message to forward.</param>
        public void Post<M>(M message)
        {
            if (message is T msgT) using (bufferLock.Lock()) buffer.Add(msgT);
            sourceHelper.Forward(message);
        }

        /// <summary>
        /// Asynchronously forwards a message and, if it is of type <typeparamref name="T"/>, adds it to the buffer first.
        /// Buffer-first semantics avoid races where a concurrently connecting sink could miss the message.
        /// </summary>
        /// <typeparam name="M">The compile-time message type.</typeparam>
        /// <param name="message">The message to forward.</param>
        public async Task PostAsync<M>(M message)
        {
            if (message is T msgT) using (await bufferLock.LockAsync().ConfiguredAwait()) buffer.Add(msgT);
            await sourceHelper.ForwardAsync(message).ConfiguredAwait();
        }

        /// <summary>
        /// Returns a snapshot copy of all currently available buffer entries in FIFO order.
        /// </summary>
        public T[] GetAllBufferEntries()
        {
            using (bufferLock.LockReadOnly())
            {
                return buffer.GetAllAvailable(0, out _, out _);
            }
        }

        /// <summary>
        /// Adds a sequence of messages directly into the buffer without forwarding them to connected sinks.
        /// </summary>
        /// <typeparam name="TEnum">The enumerable type providing the messages.</typeparam>
        /// <param name="messages">The messages to add to the buffer.</param>
        public void AddRangeToBuffer<TEnum>(TEnum messages) where TEnum : IEnumerable<T>
        {
            using (bufferLock.Lock())
            {
                foreach (var msg in messages) buffer.Add(msg);
            }
        }

        /// <summary>
        /// Connects to a sink. On connect, all buffered messages are synchronously replayed under a read-lock,
        /// then the sink is registered for live forwarding.
        /// </summary>
        /// <param name="sink">The sink to connect.</param>
        /// <param name="weakReference">If true, connect using a weak reference so the sink can be GC-collected.</param>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            using (bufferLock.LockReadOnly())
            {
                OnConnection(sink);
                sourceHelper.ConnectTo(sink, weakReference);
            }
        }

        /// <summary>
        /// Connects to another message flow connection. On connect, all buffered messages are synchronously replayed
        /// under a read-lock, then the connection is registered for live forwarding.
        /// </summary>
        /// <param name="sink">The message flow connection to connect to.</param>
        /// <param name="weakReference">If true, connect using a weak reference so the sink can be GC-collected.</param>
        /// <returns>The message source facet for the connected sink, if applicable.</returns>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            using (bufferLock.LockReadOnly())
            {
                OnConnection(sink);
                return sourceHelper.ConnectTo(sink, weakReference);
            }
        }

        /// <summary>
        /// Checks whether a specific sink is currently connected (and alive).
        /// </summary>
        /// <param name="sink">The sink to check.</param>
        /// <returns><c>true</c> if the sink is connected; otherwise, <c>false</c>.</returns>
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}