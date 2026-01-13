using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>Used to send messages of any type to all connected sinks. It is thread safe.</summary>
public sealed class Sender : IMessageSource, ISender
{
    /// <summary>Provides connection management and forwarding logic for this sender.</summary>
    private SourceValueHelper sourceHelper = new SourceValueHelper();

    /// <summary>Gets the number of currently connected sinks.</summary>
    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    /// <summary>Indicates whether there are no connected sinks.</summary>
    public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

    /// <summary>Gets all currently connected sinks.</summary>
    /// <returns>An array containing the connected sinks.</returns>
    public IMessageSink[] GetConnectedSinks()
    {
        return sourceHelper.GetConnectedSinks();
    }

    /// <summary>Disconnects all currently connected sinks.</summary>
    public void DisconnectAll()
    {
        sourceHelper.DisconnectAll();
    }

    /// <summary>Disconnects from the specified sink.</summary>
    /// <param name="sink">Sink to disconnect from.</param>
    public void DisconnectFrom(IMessageSink sink)
    {
        sourceHelper.DisconnectFrom(sink);
    }

    /// <summary>Sends a message by reference to all connected sinks.</summary>
    /// <typeparam name="T">Type of the message.</typeparam>
    /// <param name="message">Message to send.</param>
    public void Send<T>(in T message)
    {
        sourceHelper.Forward(message);
    }

    /// <summary>Sends a message to all connected sinks.</summary>
    /// <typeparam name="T">Type of the message.</typeparam>
    /// <param name="message">Message to send.</param>
    public void Send<T>(T message)
    {
        sourceHelper.Forward(message);
    }

    /// <summary>Asynchronously sends a message to all connected sinks.</summary>
    /// <typeparam name="T">Type of the message.</typeparam>
    /// <param name="message">Message to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public Task SendAsync<T>(T message)
    {
        return sourceHelper.ForwardAsync(message);
    }

    /// <summary>Connects this sender to the specified sink.</summary>
    /// <param name="sink">Sink to connect to.</param>
    /// <param name="weakReference">True to keep only a weak reference to the sink.</param>
    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {
        sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>Connects this sender to the specified flow connection.</summary>
    /// <param name="sink">Connection endpoint to connect to.</param>
    /// <param name="weakReference">True to keep only a weak reference to the sink.</param>
    /// <returns>The message source for chaining.</returns>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>Determines whether this sender is connected to the specified sink.</summary>
    /// <param name="sink">Sink to test.</param>
    /// <returns>True if the sink is connected; otherwise, false.</returns>
    public bool IsConnected(IMessageSink sink)
    {
        return sourceHelper.IsConnected(sink);
    }
}

/// <summary>Used to send messages of a specific type to all connected sinks. It is thread safe.</summary>
/// <typeparam name="T">Type of messages handled by this sender.</typeparam>
public sealed class Sender<T> : ISender<T>, IMessageSource<T>
{
    /// <summary>Provides connection management and forwarding logic for this typed sender.</summary>
    private TypedSourceValueHelper<T> sourceHelper;

    /// <summary>Gets the message type handled by this sender.</summary>
    public Type SentMessageType => sourceHelper.SentMessageType;

    /// <summary>Gets the number of currently connected sinks.</summary>
    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    /// <summary>Indicates whether there are no connected sinks.</summary>
    public bool NoConnectedSinks => sourceHelper.NotConnected;

    /// <summary>Connects this sender to the specified sink.</summary>
    /// <param name="sink">Sink to connect to.</param>
    /// <param name="weakReference">True to keep only a weak reference to the sink.</param>
    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {
        sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>Connects this sender to the specified flow connection.</summary>
    /// <param name="sink">Connection endpoint to connect to.</param>
    /// <param name="weakReference">True to keep only a weak reference to the sink.</param>
    /// <returns>The message source for chaining.</returns>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>Disconnects all currently connected sinks.</summary>
    public void DisconnectAll()
    {
        sourceHelper.DisconnectAll();
    }

    /// <summary>Disconnects from the specified sink.</summary>
    /// <param name="sink">Sink to disconnect from.</param>
    public void DisconnectFrom(IMessageSink sink)
    {
        sourceHelper.DisconnectFrom(sink);
    }

    /// <summary>Gets all currently connected sinks.</summary>
    /// <returns>An array containing the connected sinks.</returns>
    public IMessageSink[] GetConnectedSinks()
    {
        return sourceHelper.GetConnectedSinks();
    }

    /// <summary>Determines whether this sender is connected to the specified sink.</summary>
    /// <param name="sink">Sink to test.</param>
    /// <returns>True if the sink is connected; otherwise, false.</returns>
    public bool IsConnected(IMessageSink sink)
    {
        return sourceHelper.IsConnected(sink);
    }

    /// <summary>Sends a message by reference to all connected sinks.</summary>
    /// <param name="message">Message to send.</param>
    public void Send(in T message)
    {
        sourceHelper.Forward(in message);
    }

    /// <summary>Sends a message to all connected sinks.</summary>
    /// <param name="message">Message to send.</param>
    public void Send(T message)
    {
        sourceHelper.Forward(message);
    }

    /// <summary>Asynchronously sends a message to all connected sinks.</summary>
    /// <param name="message">Message to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public Task SendAsync(T message)
    {
        return sourceHelper.ForwardAsync(message);
    }
}