using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Represents a generic wrapper around a payload message.
    /// Useful when additional metadata or a common abstraction is needed without
    /// constraining the payload type.
    /// </summary>
    /// <remarks>
    /// Implementations should ensure that the returned <see cref="Message"/> matches
    /// the typed view (when implemented together with <see cref="IMessageWrapper{T}"/>).
    /// For value types, accessing <see cref="Message"/> will box the value; prefer
    /// the typed interface to avoid boxing in performance-sensitive paths.
    /// </remarks>
    public interface IMessageWrapper
    {
        /// <summary>
        /// The wrapped message as an object.
        /// </summary>
        /// <remarks>
        /// May cause boxing for value types. If possible, consume via
        /// <see cref="IMessageWrapper{T}.TypedMessage"/> to avoid boxing.
        /// </remarks>
        object Message { get; }

        /// <summary>
        /// Unwraps the message and forwards it to the given sink.
        /// </summary>
        /// <param name="sender">The sender to send the message to.</param>
        void UnwrapAndSend(ISender sender);

        /// <summary>
        /// Unwraps the message and forwards it by reference to the given sink.
        /// </summary>
        /// <param name="sender">The sender to send the message to.</param>
        void UnwrapAndSendByRef(ISender sender);

        /// <summary>
        /// Unwraps the message and forwards it asynchronously to the given sink.
        /// </summary>
        /// <param name="sender">The sender to send the message to.</param>
        Task UnwrapAndSendAsync(ISender sender);
    }

    /// <summary>
    /// Represents a typed wrapper around a payload message.
    /// </summary>
    /// <typeparam name="T">Type of the wrapped message.</typeparam>
    /// <remarks>
    /// Implementations should keep <see cref="TypedMessage"/> consistent with
    /// <see cref="IMessageWrapper.Message"/> (i.e., <c>object.ReferenceEquals(TypedMessage, Message)</c>
    /// for reference types, and value equality for value types).
    /// Prefer immutable implementations to avoid data races in concurrent flows.
    /// </remarks>
    public interface IMessageWrapper<T> : IMessageWrapper
    {
        /// <summary>
        /// The wrapped message with its concrete type.
        /// </summary>
        T TypedMessage { get; }
    }

    /// <summary>
    /// Represents a wrapper for a message associated with a specific topic.
    /// </summary>
    /// <remarks>This class provides a way to encapsulate a message along with its associated topic, enabling
    /// scenarios where messages need to be categorized, filtered, or routed based on their topic.</remarks>
    /// <typeparam name="T">The type of the message being wrapped.</typeparam>
    public sealed class TopicMessageWrapper<T> : IMessageWrapper<T>, ITopicMessage
    {
        public TopicMessageWrapper(T message, string topic)
        {
            TypedMessage = message;
            Topic = topic;
        }
        public T TypedMessage { get; }
        public object Message => TypedMessage;
        public string Topic { get; }

        public void UnwrapAndSend(ISender sender)
        {
            sender.Send(TypedMessage);
        }

        public Task UnwrapAndSendAsync(ISender sender)
        {
            return sender.SendAsync(TypedMessage);
        }

        public void UnwrapAndSendByRef(ISender sender)
        {
            T message = TypedMessage;
            sender.Send(in message);
        }
    }
}