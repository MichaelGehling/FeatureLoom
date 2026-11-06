using FeatureLoom.MessageFlow;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Message sink that appends all received messages of type <typeparamref name="T"/> to a <see cref="LogWriter"/>.
/// </summary>
/// <remarks>
/// <see cref="Post{M}(in M)"/> queues the entry for the writer's group commit and returns immediately;
/// <see cref="PostAsync{M}(M)"/> completes after the entry was written. With an error handler, write errors of both are passed to it
/// (the task returned by <see cref="PostAsync{M}(M)"/> then does not fault); without one, <see cref="PostAsync{M}(M)"/> faults. Messages of other types are ignored.
/// Write errors of fire-and-forget posts are passed to the optional error handler; without one they are not observed.
/// </remarks>
/// <typeparam name="T">Prepared payload type of the log.</typeparam>
public sealed class LogWriterSink<T> : IMessageSink<T>, ILogAppendCallback
{
    private readonly LogWriter writer;
    private readonly Action<Exception> onError;
    private readonly Action<Task> observeError;

    /// <summary>Creates a sink appending to <paramref name="writer"/>.</summary>
    /// <param name="writer">Target log writer.</param>
    /// <param name="onError">Optional handler for write errors of <c>Post</c> and <c>PostAsync</c> calls.</param>
    public LogWriterSink(LogWriter writer, Action<Exception> onError = null)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.onError = onError;
        observeError = task => { if (task.IsFaulted) this.onError(task.Exception.InnerException ?? task.Exception); };
    }

    /// <inheritdoc/>
    public Type ConsumedMessageType => typeof(T);

    /// <inheritdoc/>
    public void Post<M>(in M message)
    {
        if (message is T payload) Queue(payload);
    }

    /// <inheritdoc/>
    public void Post<M>(M message)
    {
        if (message is T payload) Queue(payload);
    }

    /// <inheritdoc/>
    public Task PostAsync<M>(M message)
    {
        if (!(message is T payload)) return Task.CompletedTask;
        var task = writer.AppendAsync(payload);
        // With an error handler, the fault is handed to it and the returned task completes normally after the write attempt.
        return onError != null ? task.ContinueWith(observeError, TaskContinuationOptions.ExecuteSynchronously) : task;
    }

    // Task-free enqueue: fire-and-forget posts allocate nothing per entry (no Task/TaskCompletionSource).
    private void Queue(T payload) => writer.Enqueue(payload, onError != null ? this : null);

    void ILogAppendCallback.OnAppended(LogEntryId id, object state) { }

    void ILogAppendCallback.OnFailed(Exception error, object state) => onError(error);
}
