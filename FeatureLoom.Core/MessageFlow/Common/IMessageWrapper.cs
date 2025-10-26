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
}