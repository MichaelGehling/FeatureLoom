using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Marker interface for all message flow elements (sources, sinks, and connections).
    /// </summary>
    public interface IMessageFlow { }

    /// <summary>
    /// Receives messages.
    /// Implementations should be thread-safe unless explicitly documented otherwise.
    /// </summary>
    public interface IMessageSink : IMessageFlow
    {
        /// <summary>
        /// Posts a message to the sink by reference.
        /// Prefer this for performance-sensitive paths to avoid unnecessary copies for large structs.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message to post.</param>
        void Post<M>(in M message);

        /// <summary>
        /// Posts a message to the sink by value.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message to post.</param>
        void Post<M>(M message);

        /// <summary>
        /// Posts a message to the sink asynchronously.
        /// Implementations should not block the caller and should honor ordering guarantees they advertise.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message to post.</param>
        Task PostAsync<M>(M message);
    }

    /// <summary>
    /// Produces messages and manages connections to sinks.
    /// </summary>
    public interface IMessageSource : IMessageFlow
    {
        /// <summary>
        /// Connects this source to a sink.
        /// When <paramref name="weakReference"/> is true, the connection is held weakly (GC can collect the sink).
        /// </summary>
        void ConnectTo(IMessageSink sink, bool weakReference = false);

        /// <summary>
        /// Connects this source to a bidirectional element (sink+source),
        /// returning the same element typed as a source to enable fluent chaining.
        /// </summary>
        IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false);

        /// <summary>
        /// Disconnects a sink from this source.
        /// </summary>
        void DisconnectFrom(IMessageSink sink);

        /// <summary>
        /// Disconnects all sinks from this source.
        /// </summary>
        void DisconnectAll();

        /// <summary>
        /// Number of currently connected sinks (excluding already collected weak refs).
        /// </summary>
        int CountConnectedSinks { get; }

        /// <summary>
        /// True if there are no connected sinks.
        /// </summary>
        bool NoConnectedSinks { get; }

        /// <summary>
        /// Returns the currently connected sinks (invalid weak refs are pruned lazily).
        /// </summary>
        IMessageSink[] GetConnectedSinks();

        /// <summary>
        /// Checks whether the provided sink is connected.
        /// </summary>
        bool IsConnected(IMessageSink sink);
    }

    /// <summary>
    /// A sink that declares the type it consumes. This enables safer graph building and runtime validation.
    /// </summary>
    public interface ITypedMessageSink : IMessageSink
    {
        /// <summary>
        /// The CLR type this sink consumes.
        /// </summary>
        Type ConsumedMessageType { get; }
    }

    /// <summary>
    /// Typed sink marker that also implements <see cref="ITypedMessageSink"/>.
    /// </summary>
    public interface IMessageSink<T> : ITypedMessageSink
    {
    }

    /// <summary>
    /// A source that declares the type it produces.
    /// </summary>
    public interface ITypedMessageSource : IMessageSource
    {
        /// <summary>
        /// The CLR type this source emits.
        /// </summary>
        Type SentMessageType { get; }
    }

    /// <summary>
    /// Typed source marker that also implements <see cref="ITypedMessageSource"/>.
    /// </summary>
    public interface IMessageSource<T> : ITypedMessageSource
    {
    }

    /// <summary>
    /// Combines sink and source roles in a single element (e.g., filters, forwarders, transformers).
    /// </summary>
    public interface IMessageFlowConnection : IMessageSink, IMessageSource
    {
    }

    /// <summary>
    /// Typed connection where input and output types are the same.
    /// </summary>
    public interface IMessageFlowConnection<T> : IMessageFlowConnection, IMessageSink<T>, IMessageSource<T>
    {
    }

    /// <summary>
    /// Typed connection where input and output types differ (e.g., transforms).
    /// </summary>
    public interface IMessageFlowConnection<I, O> : IMessageFlowConnection, IMessageSink<I>, IMessageSource<O>
    {
    }

    /// <summary>
    /// Provides an alternative source route (e.g., for fallback/error paths).
    /// </summary>
    public interface IAlternativeMessageSource
    {
        IMessageSource Else { get; }
    }

    /// <summary>
    /// A bidirectional participant that can reply to requests (source+sink).
    /// </summary>
    public interface IReplier : IMessageSource, IMessageSink
    {
    }

    /// <summary>
    /// A bidirectional participant that can send requests and receive responses.
    /// </summary>
    public interface IRequester : IMessageSource, IMessageSink
    {
        /// <summary>
        /// Connects the requester to a replier and also connects the replier back to the requester
        /// to establish a two-way channel. <paramref name="weakReference"/> controls weak reference usage.
        /// </summary>
        void ConnectToAndBack(IReplier replier, bool weakReference = false);
    }

    /// <summary>
    /// Represents a request message carrying a request id and payload.
    /// </summary>
    public interface IRequestMessage<T>
    {
        /// <summary>Identifier correlating request and response.</summary>
        public long RequestId { get; set; }

        /// <summary>Payload of the request.</summary>
        public T Content { get; }
    }

    /// <summary>
    /// Represents a response message carrying a request id and payload.
    /// </summary>
    public interface IResponseMessage<T>
    {
        /// <summary>Identifier correlating this response to its request.</summary>
        public long RequestId { get; set; }

        /// <summary>Payload of the response.</summary>
        public T Content { get; }
    }
}