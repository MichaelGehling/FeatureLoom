using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Abstraction for broadcasting messages to connected sinks.
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Sends a message by reference to connected sinks.
        /// Prefer this overload for large structs to avoid copies on the call site.
        /// </summary>
        /// <typeparam name="T">Type of the message.</typeparam>
        /// <param name="message">The message to send.</param>
        void Send<T>(in T message);

        /// <summary>
        /// Sends a message by value to connected sinks.
        /// </summary>
        /// <typeparam name="T">Type of the message.</typeparam>
        /// <param name="message">The message to send.</param>
        void Send<T>(T message);

        /// <summary>
        /// Sends a message asynchronously to connected sinks.
        /// Implementations should not block the caller and should forward to sinks using their async paths.
        /// Ordering guarantees are implementation-specific.
        /// </summary>
        /// <typeparam name="T">Type of the message.</typeparam>
        /// <param name="message">The message to send.</param>
        Task SendAsync<T>(T message);
    }

    /// <summary>
    /// Abstraction for broadcasting messages of a specific type to connected sinks.
    /// </summary>
    /// <typeparam name="T">Type of the messages this sender emits.</typeparam>
    public interface ISender<T>
    {
        /// <summary>
        /// Sends a message by reference to connected sinks.
        /// Prefer this overload for large structs to avoid copies on the call site.
        /// </summary>
        /// <param name="message">The message to send.</param>
        void Send(in T message);

        /// <summary>
        /// Sends a message by value to connected sinks.
        /// </summary>
        /// <param name="message">The message to send.</param>
        void Send(T message);

        /// <summary>
        /// Sends a message asynchronously to connected sinks.
        /// Implementations should not block the caller and should forward to sinks using their async paths.
        /// Ordering guarantees are implementation-specific.
        /// </summary>
        /// <param name="message">The message to send.</param>
        Task SendAsync(T message);
    }
}