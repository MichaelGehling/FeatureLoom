using FeatureLoom.Helpers;
using System;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Represents a request message that carries a correlation identifier and a payload of type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// - The <see cref="RequestId"/> correlates a request with its corresponding <see cref="ResponseMessage{T}"/>.
    /// - A request id is generated automatically when using <see cref="RequestMessage{T}(T)"/>; you can also provide one explicitly.
    /// - This type is mutable and not thread-safe; avoid mutating <see cref="Content"/> or <see cref="RequestId"/> once the message is in-flight.
    /// - The parameterless constructor exists primarily for serializers.
    /// </remarks>
    /// <typeparam name="T">The payload type carried by the request.</typeparam>
    public class RequestMessage<T> : IRequestMessage<T>
    {
        T content;
        long requestId;

        /// <summary>
        /// Creates a request with the specified payload and correlation id.
        /// </summary>
        /// <param name="content">The request payload.</param>
        /// <param name="requestId">The correlation identifier to associate with this request.</param>
        public RequestMessage(T content, long requestId)
        {
            this.content = content;
            this.requestId = requestId;
        }

        /// <summary>
        /// Creates a request with the specified payload and an auto-generated correlation id.
        /// </summary>
        /// <param name="content">The request payload.</param>
        public RequestMessage(T content)
        {
            this.content = content;
            this.requestId = RandomGenerator.Int64();
        }

        /// <summary>
        /// Initializes a new instance for serialization scenarios.
        /// </summary>
        public RequestMessage()
        {
        }

        /// <summary>
        /// Identifier correlating this request with its response.
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
        /// Creates a response message that carries the provided payload and the same <see cref="RequestId"/> as this request.
        /// </summary>
        /// <typeparam name="RESP">The response payload type.</typeparam>
        /// <param name="content">The response payload.</param>
        /// <returns>A <see cref="ResponseMessage{RESP}"/> correlated to this request.</returns>
        public ResponseMessage<RESP> CreateResponse<RESP>(RESP content)
        {
            return new ResponseMessage<RESP>(content, requestId);
        }

        /// <summary>
        /// The request payload.
        /// </summary>
        public T Content
        {
            get => content;
            set => content = value;
        }

        /// <summary>
        /// Implicitly converts the request to its payload.
        /// </summary>
        /// <remarks>
        /// This does not expose the <see cref="RequestId"/>. Use with care to avoid losing correlation context.
        /// </remarks>
        /// <param name="req">The request message.</param>
        public static implicit operator T(RequestMessage<T> req) => req.content;
    }
}
