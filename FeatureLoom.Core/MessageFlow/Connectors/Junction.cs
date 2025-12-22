using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Routes incoming messages to one or multiple target sinks based on runtime type and an optional predicate.
/// </summary>
/// <remarks>
/// Matching:
/// - Each option matches when <c>message is T</c> and (predicate is null OR predicate(message) == true).
/// Dispatch:
/// - If <c>multiOption == false</c>, options are sorted by descending priority and the first match wins.
/// - If <c>multiOption == true</c>, all matching options receive the message (order = sorted order since sorting is always applied here).
/// Fallback:
/// - If no option matches and <see cref="Else"/> was accessed (lazy creation), message is forwarded to the alternative source.
/// Thread-safety:
/// - Options are added under a lock using copy-on-write and read lock-free via a snapshot.
/// Performance:
/// - Provides both by-ref (<see cref="Post{M}(in M)"/>) and by-value (<see cref="Post{M}(M)"/>) posting overloads.
/// Async:
/// - Multi-option async path awaits each sink sequentially using <c>.ConfiguredAwait()</c>.
/// </remarks>
public sealed class Junction : IMessageSink, IAlternativeMessageSource
{
    /// <summary>
    /// Internal option abstraction used for heterogeneous type matching.
    /// </summary>
    private interface IOption
    {
        bool CheckMessage<M>(in M message);
        IMessageSink Sink { get; }
        int Priority { get; }
    }

    /// <summary>
    /// Concrete option for messages of type <typeparamref name="T"/>
    /// </summary>
    private sealed class Option<T> : IOption
    {
        private readonly IMessageSink sink;
        public IMessageSink Sink => sink;

        private readonly Func<T, bool> checkMessage;

        public int Priority { get; }

        public Option(IMessageSink sink, Func<T, bool> checkMessage, int priority)
        {
            this.sink = sink;
            this.checkMessage = checkMessage;
            Priority = priority;
        }

        /// <summary>
        /// Returns true when the runtime type matches <typeparamref name="T"/> and the optional predicate passes.
        /// </summary>
        public bool CheckMessage<M>(in M message)
        {
            if (message is T msgT)
            {
                if (checkMessage == null) return true;
                return checkMessage(msgT);
            }
            return false;
        }
    }

    private IOption[] options = Array.Empty<IOption>();
    private readonly bool multiOption;
    private readonly MicroLock optionsLock = new MicroLock();
    private LazyValue<SourceHelper> alternativeSendingHelper;

    /// <summary>
    /// Fallback message source. Messages not matched by any option are forwarded here.
    /// Accessing this property creates it.
    /// </summary>
    public IMessageSource Else => alternativeSendingHelper.Obj;

    /// <summary>
    /// Creates a new junction.
    /// </summary>
    /// <param name="multiOption">
    /// False: only highest priority matching option receives the message.
    /// True: all matching options receive the message.
    /// </param>
    public Junction(bool multiOption = false)
    {
        this.multiOption = multiOption;
    }

    /// <summary>
    /// Adds a new option for messages of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Message type to match.</typeparam>
    /// <param name="sink">Target sink receiving matched messages.</param>
    /// <param name="checkMessage">Optional predicate. If null all messages of type <typeparamref name="T"/> match.</param>
    /// <param name="priority">Higher values sort earlier. Sorting applies regardless of <c>multiOption</c> in current implementation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/> is null.</exception>
    public void ConnectOption<T>(IMessageSink sink, Func<T, bool> checkMessage = null, int priority = 0)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));

        using (optionsLock.Lock())
        {
            var localOptions = options;
            var newOptions = new IOption[localOptions.Length + 1];
            Array.Copy(localOptions, newOptions, localOptions.Length);
            newOptions[newOptions.Length - 1] = new Option<T>(sink, checkMessage, priority);
            // Sorted descending so highest priority first.
            Array.Sort(newOptions, (a, b) => b.Priority.CompareTo(a.Priority));
            options = newOptions;
        }
    }

    /// <summary>
    /// Adds a new option for messages of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Message type to match.</typeparam>
    /// <param name="sink">Target sink receiving matched messages.</param>
    /// <param name="checkMessage">Optional predicate. If null all messages of type <typeparamref name="T"/> match.</param>
    /// <param name="priority">Higher values sort earlier. Sorting applies regardless of <c>multiOption</c> in current implementation.</param>
    /// <returns>The connected sink for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/> is null.</exception>
    public IMessageSource ConnectOption<T>(IMessageFlowConnection sink, Func<T, bool> checkMessage = null, int priority = 0)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));

        using (optionsLock.Lock())
        {
            var localOptions = options;
            var newOptions = new IOption[localOptions.Length + 1];
            Array.Copy(localOptions, newOptions, localOptions.Length);
            newOptions[newOptions.Length - 1] = new Option<T>(sink, checkMessage, priority);
            // Sorted descending so highest priority first.
            Array.Sort(newOptions, (a, b) => b.Priority.CompareTo(a.Priority));
            options = newOptions;
        }
        return sink;
    }

    /// <summary>
    /// Adds a new option for messages of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Message type to match.</typeparam>
    /// <param name="checkMessage">Optional predicate. If null all messages of type <typeparamref name="T"/> match.</param>
    /// <param name="priority">Higher values sort earlier. Sorting applies regardless of <c>multiOption</c> in current implementation.</param>
    /// <returns>A newly connected option forwarder</returns>
    public IMessageSource<T> ConnectOption<T>(Func<T, bool> checkMessage = null, int priority = 0)
    {
        IMessageFlowConnection<T> sink = new Forwarder<T>();
        using (optionsLock.Lock())
        {
            var localOptions = options;
            var newOptions = new IOption[localOptions.Length + 1];
            Array.Copy(localOptions, newOptions, localOptions.Length);
            newOptions[newOptions.Length - 1] = new Option<T>(sink, checkMessage, priority);
            // Sorted descending so highest priority first.
            Array.Sort(newOptions, (a, b) => b.Priority.CompareTo(a.Priority));
            options = newOptions;
        }
        return sink;
    }

    /// <summary>
    /// Posts a message by reference to matching sinks (single or multiple).
    /// </summary>
    /// <typeparam name="M">Runtime type of the message.</typeparam>
    /// <param name="message">Message passed by readonly reference.</param>
    public void Post<M>(in M message)
    {
        var localOptions = options;
        bool forwarded = false;
        foreach (var option in localOptions)
        {
            if (option.CheckMessage(in message))
            {
                option.Sink.Post(in message);
                forwarded = true;
                if (!multiOption) return;
            }
        }
        if (!forwarded && alternativeSendingHelper.Exists)
        {
            alternativeSendingHelper.Obj.Forward(in message);
        }
    }

    /// <summary>
    /// Posts a message by value to matching sinks.
    /// </summary>
    /// <typeparam name="M">Runtime type of the message.</typeparam>
    /// <param name="message">Message instance.</param>
    public void Post<M>(M message)
    {
        var localOptions = options;
        bool forwarded = false;
        foreach (var option in localOptions)
        {
            if (option.CheckMessage(message))
            {
                option.Sink.Post(message);
                forwarded = true;
                if (!multiOption) return;
            }
        }
        if (!forwarded && alternativeSendingHelper.Exists)
        {
            alternativeSendingHelper.Obj.Forward(message);
        }
    }

    /// <summary>
    /// Asynchronously posts a message. Returns after first match in single-option mode, otherwise awaits each matching sink sequentially.
    /// </summary>
    /// <typeparam name="M">Runtime type of the message.</typeparam>
    /// <param name="message">Message instance.</param>
    /// <returns>A task representing completion of forwarding (or Task.CompletedTask if unmatched and no fallback).</returns>
    public Task PostAsync<M>(M message)
    {
        var localOptions = options;
        if (!multiOption || localOptions.Length <= 1)
        {
            foreach (var option in localOptions)
            {
                if (option.CheckMessage(message))
                {
                    return option.Sink.PostAsync(message);
                }
            }
            if (alternativeSendingHelper.Exists)
            {
                return alternativeSendingHelper.Obj.ForwardAsync(message);
            }
        }
        else return PostMultiOptionsAsync(message, localOptions);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Async multi-option dispatch (sequential). Uses <c>.ConfiguredAwait()</c> extension for await configuration.
    /// </summary>
    private async Task PostMultiOptionsAsync<M>(M message, IOption[] localOptions)
    {
        bool forwarded = false;
        foreach (var option in localOptions)
        {
            if (option.CheckMessage(message))
            {
                await option.Sink.PostAsync(message).ConfiguredAwait();
                forwarded = true;
            }
        }
        if (!forwarded && alternativeSendingHelper.Exists)
        {
            await alternativeSendingHelper.Obj.ForwardAsync(message).ConfiguredAwait();
        }
    }
}