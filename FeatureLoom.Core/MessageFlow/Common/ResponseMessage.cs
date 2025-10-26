namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Represents a response message carrying a correlation identifier and a payload of type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// - The <see cref="RequestId"/> correlates this response to its originating request.
    /// - This type is mutable and not thread-safe; avoid mutating <see cref="Content"/> or <see cref="RequestId"/> once the message is in-flight.
    /// - The parameterless constructor exists primarily for serializers.
    /// - An implicit conversion to <typeparamref name="T"/> is provided for convenience; it does not carry the <see cref="RequestId"/>.
    /// </remarks>
    /// <typeparam name="T">The payload type carried by the response.</typeparam>
    public class ResponseMessage<T> : IResponseMessage<T>
    {
        private T content;
        private long requestId;

        /// <summary>
        /// Creates a response with the specified payload and correlation id.
        /// </summary>
        /// <param name="content">The response payload.</param>
        /// <param name="requestId">The correlation identifier that matches the originating request.</param>
        public ResponseMessage(T content, long requestId)
        {
            this.content = content;
            this.requestId = requestId;
        }

        /// <summary>
        /// Initializes a new instance for serialization scenarios.
        /// </summary>
        public ResponseMessage()
        {
        }

        /// <summary>
        /// The response payload.
        /// </summary>
        public T Content
        {
            get => content;
            set => content = value;
        }

        /// <summary>
        /// Identifier correlating this response to its request.
        /// </summary>
        /// <remarks>
        /// Changing the value after sending can break correlation; prefer setting it once during construction.
        /// </remarks>
        public long RequestId
        {
            get => requestId;
            set => requestId = value;
        }

        /// <summary>
        /// Implicitly converts the response to its payload.
        /// </summary>
        /// <remarks>
        /// This conversion discards the <see cref="RequestId"/>. Use with care to avoid losing correlation context.
        /// </remarks>
        /// <param name="res">The response message.</param>
        public static implicit operator T(ResponseMessage<T> res) => res.content;
    }
}
