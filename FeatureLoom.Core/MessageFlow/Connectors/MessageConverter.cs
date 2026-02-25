using FeatureLoom.Extensions;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Converts messages of type <typeparamref name="I"/> to <typeparamref name="O"/> using the provided converter
/// and forwards the result to connected sinks. Messages not assignable to <typeparamref name="I"/> can optionally
/// be forwarded unchanged.
/// </summary>
/// <remarks>
/// THREADING
/// - Thread-safe assuming the supplied <see cref="Func{I, O}"/> is thread-safe.
///
/// FORWARDING BEHAVIOR
/// - Uses by-ref forwarding for non-numeric value-type outputs to reduce copies; otherwise forwards by value.
/// - Ordering and connection semantics are delegated to <see cref="SourceValueHelper"/>.
///
/// NULL HANDLING
/// - A null message will not match <typeparamref name="I"/> (for reference types) and will therefore not be converted.
///   If <c>forwardOtherMessages</c> is true, the null is forwarded unchanged.
/// </remarks>
/// <typeparam name="I">Input message type the converter accepts.</typeparam>
/// <typeparam name="O">Output message type produced by the converter.</typeparam>
public sealed class MessageConverter<I, O> : IMessageFlowConnection<I,O>
{
    /// <summary>
    /// Manages connections and forwarding to sinks. See <see cref="SourceValueHelper"/> for details.
    /// </summary>
    private SourceValueHelper sourceHelper = new SourceValueHelper();

    /// <summary>
    /// Converter function applied to messages assignable to <typeparamref name="I"/>.
    /// </summary>
    private readonly Func<I, O> convertFunc;

    /// <summary>
    /// When true, messages not assignable to <typeparamref name="I"/> are forwarded unchanged.
    /// </summary>
    private readonly bool forwardOtherMessages;

    /// <summary>
    /// Decides whether to forward outputs by reference to avoid copies (true for non-numeric value types).
    /// Computed once per closed generic type.
    /// </summary>
    private static readonly bool forwardOutputByRef = ComputeForwardOutputByRef();

    /// <summary>
    /// The type of messages this source sends downstream (<typeparamref name="O"/>).
    /// </summary>
    public Type SentMessageType => typeof(O);

    /// <summary>
    /// The type of messages this element consumes for conversion (<typeparamref name="I"/>).
    /// </summary>
    public Type ConsumedMessageType => typeof(I);

    /// <summary>
    /// Creates a new message converter.
    /// </summary>
    /// <param name="convertFunc">Converter function applied to messages assignable to <typeparamref name="I"/>.</param>
    /// <param name="forwardOtherMessages">
    /// When true, messages not assignable to <typeparamref name="I"/> are forwarded unchanged to connected sinks.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="convertFunc"/> is null.</exception>
    public MessageConverter(Func<I, O> convertFunc, bool forwardOtherMessages = true)
    {
        this.convertFunc = convertFunc ?? throw new ArgumentNullException(nameof(convertFunc));
        this.forwardOtherMessages = forwardOtherMessages;
    }

    /// <summary>
    /// Computes whether outputs of type <typeparamref name="O"/> should be forwarded by reference.
    /// Returns true for non-numeric value types; false for reference types, enums, and numeric types (incl. Nullable of numeric).
    /// </summary>
    private static bool ComputeForwardOutputByRef()
    {
        var t = Nullable.GetUnderlyingType(typeof(O)) ?? typeof(O);
        if (!t.IsValueType) return false;         // reference types: by value
        if (t.IsEnum) return false;               // enums: small, by value
        return !t.IsNumericType();                // non-numeric structs: by ref
    }

    /// <summary>
    /// Number of currently connected sinks (excluding already collected weak refs).
    /// </summary>
    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    /// <summary>
    /// Indicates whether there are no connected sinks.
    /// </summary>
    public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

    /// <summary>
    /// Returns the currently connected sinks (invalid weak refs are pruned lazily).
    /// </summary>
    public IMessageSink[] GetConnectedSinks() => sourceHelper.GetConnectedSinks();

    /// <summary>
    /// Disconnects all currently connected sinks.
    /// </summary>
    public void DisconnectAll() => sourceHelper.DisconnectAll();

    /// <summary>
    /// Disconnects the specified sink if connected.
    /// </summary>
    /// <param name="sink">Sink to disconnect.</param>
    public void DisconnectFrom(IMessageSink sink) => sourceHelper.DisconnectFrom(sink);

    /// <summary>
    /// Posts a message by reference.
    /// If the message is assignable to <typeparamref name="I"/>, converts it and forwards the result;
    /// otherwise, forwards the original message unchanged when <c>forwardOtherMessages</c> is true.
    /// </summary>
    /// <typeparam name="M">Type of the message instance.</typeparam>
    /// <param name="message">Message to post.</param>
    public void Post<M>(in M message)
    {
        if (message is I msgT)
        {
            O output = convertFunc(msgT);
            if (forwardOutputByRef) sourceHelper.Forward(in output);
            else sourceHelper.Forward(output);
        }
        else if (forwardOtherMessages)
        {
            sourceHelper.Forward(in message);
        }
    }

    /// <summary>
    /// Posts a message by value.
    /// If the message is assignable to <typeparamref name="I"/>, converts it and forwards the result;
    /// otherwise, forwards the original message unchanged when <c>forwardOtherMessages</c> is true.
    /// </summary>
    /// <typeparam name="M">Type of the message instance.</typeparam>
    /// <param name="message">Message to post.</param>
    public void Post<M>(M message)
    {
        if (message is I msgT)
        {
            O output = convertFunc(msgT);
            if (forwardOutputByRef) sourceHelper.Forward(in output);
            else sourceHelper.Forward(output);
        }
        else if (forwardOtherMessages)
        {
            sourceHelper.Forward(in message);
        }
    }

    /// <summary>
    /// Asynchronously posts a message.
    /// If the message is assignable to <typeparamref name="I"/>, converts it and forwards the result;
    /// otherwise, forwards the original message unchanged when <c>forwardOtherMessages</c> is true.
    /// </summary>
    /// <typeparam name="M">Type of the message instance.</typeparam>
    /// <param name="message">Message to post.</param>
    /// <returns>
    /// A task that completes when forwarding finishes. If nothing is forwarded (no match and forwarding disabled),
    /// returns a completed task.
    /// </returns>
    public Task PostAsync<M>(M message)
    {
        if (message is I msgT) return sourceHelper.ForwardAsync(convertFunc(msgT));
        else if (forwardOtherMessages) return sourceHelper.ForwardAsync(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Connects this source to a sink.
    /// When <paramref name="weakReference"/> is true, the connection is held weakly so the sink can be GC-collected.
    /// </summary>
    /// <param name="sink">Sink to connect to.</param>
    /// <param name="weakReference">True to hold a weak reference to the sink.</param>
    public void ConnectTo(IMessageSink sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    /// <summary>
    /// Connects this source to a bidirectional flow element and returns the same element typed as a source
    /// to enable fluent chaining.
    /// </summary>
    /// <param name="sink">The element to connect to.</param>
    /// <param name="weakReference">True to hold a weak reference to the element.</param>
    /// <returns>The provided <paramref name="sink"/> typed as <see cref="IMessageSource"/>.</returns>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    /// <summary>
    /// Checks whether the specified sink is currently connected and alive.
    /// </summary>
    /// <param name="sink">Sink to check.</param>
    /// <returns>True when connected; otherwise false.</returns>
    public bool IsConnected(IMessageSink sink) => sourceHelper.IsConnected(sink);
}