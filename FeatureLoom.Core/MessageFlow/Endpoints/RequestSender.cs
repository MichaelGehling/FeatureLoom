using FeatureLoom.Helpers;
using FeatureLoom.Scheduling;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Sends request messages of type <typeparamref name="REQ"/> and correlates incoming responses of type <typeparamref name="RESP"/>.
/// Implements both source (for outgoing requests) and sink (for incoming responses) roles, handling timeouts and correlation.
/// </summary>
/// <typeparam name="REQ">Request payload type.</typeparam>
/// <typeparam name="RESP">Response payload type.</typeparam>
public sealed class RequestSender<REQ, RESP> : IMessageSource<IRequestMessage<REQ>>, IMessageSink<IResponseMessage<RESP>>, IRequester
{
    TypedSourceValueHelper<IRequestMessage<REQ>> sourceHelper = new();
    List<ResponseHandler> responseHandlers = new List<ResponseHandler>();
    MicroLock responseHandlerLock = new MicroLock();
    TimeSpan timeout = 1.Seconds();
    short senderId = RandomGenerator.Int16();
    ActionSchedule timeoutSchedule = null;
    readonly bool preciseTimeout = false;

    /// <summary>
    /// Creates a requester with a custom response timeout.
    /// </summary>
    /// <param name="timeout">Maximum duration to wait for a response before canceling the request.</param>
    public RequestSender(TimeSpan timeout, bool preciseTimeout = false)
    {
        this.timeout = timeout;
        this.preciseTimeout = preciseTimeout;
    }

    /// <summary>
    /// Creates a requester with the default timeout (1 second).
    /// </summary>
    public RequestSender()
    {
    }

    private void StartTimeoutCheck()
    {
        if (timeoutSchedule != null) return;

        timeoutSchedule = Service<SchedulerService>.Instance.ScheduleAction("RequestSenderTimeout", now =>
        {
            if (responseHandlers.Count == 0 && !responseHandlerLock.IsLocked)
            {
                timeoutSchedule = null;
                return ScheduleStatus.Terminated;
            }

            using (responseHandlerLock.Lock())
            {
                if (responseHandlers.Count == 0)
                {
                    timeoutSchedule = null;
                    return ScheduleStatus.Terminated;
                }

                var nowEffective = preciseTimeout ? AppTime.Now : now;

                for (int i = 0; i < responseHandlers.Count; i++)
                {
                    if (responseHandlers[i].requestTime + timeout > nowEffective) break;
                    responseHandlers[i].tcs.TrySetCanceled();
                    responseHandlers.RemoveAt(i--);
                }

                if (responseHandlers.Count == 0)
                {
                    timeoutSchedule = null;
                    return ScheduleStatus.Terminated;
                }

                return ScheduleStatus.WaitFor((responseHandlers[0].requestTime + timeout) - nowEffective);
            }

        });
    }

    /// <summary>
    /// Sends a request asynchronously and awaits the correlated response.
    /// </summary>
    /// <param name="message">Request payload.</param>
    /// <returns>Task that completes with the response payload or is canceled on timeout.</returns>
    public async Task<RESP> SendRequestAsync(REQ message)
    {
        var handler = ResponseHandler.Create(CreateRequestId(), GetNow());

        using (responseHandlerLock.Lock())
        {
            responseHandlers.Add(handler);
            StartTimeoutCheck();
        }

        var request = new RequestMessage<REQ>(message, handler.requestId);

        await sourceHelper.ForwardAsync(request);
        return await handler.tcs.Task;
    }

    private DateTime GetNow()
    {
        return preciseTimeout ? AppTime.Now : AppTime.CoarseNow;
    }

    /// <summary>
    /// Sends a request synchronously and blocks for the correlated response.
    /// </summary>
    /// <param name="message">Request payload.</param>
    /// <returns>The response payload.</returns>
    public RESP SendRequest(REQ message)
    {
        var handler = ResponseHandler.Create(CreateRequestId(), GetNow());

        using (responseHandlerLock.Lock())
        {
            responseHandlers.Add(handler);
            StartTimeoutCheck();
        }

        var request = new RequestMessage<REQ>(message, handler.requestId);

        sourceHelper.Forward(request);
        return handler.tcs.Task.GetAwaiter().GetResult();
    }

    private void HandleResponse(RESP response, long requestId)
    {
        using (responseHandlerLock.Lock())
        {
            if (responseHandlers.Count == 0) return;
            if (responseHandlers.Count == 1)
            {
                var handler = responseHandlers[0];
                if (handler.requestId != requestId) return;
                handler.tcs.TrySetResult(response);
                responseHandlers.Clear();
                return;
            }

            for (int i = responseHandlers.Count - 1; i >= 0; i--)
            {
                var handler = responseHandlers[i];
                if (handler.requestId != requestId) continue;
                handler.tcs.TrySetResult(response);
                responseHandlers.RemoveAt(i);
                return;
            }
        }
    }

    private long CreateRequestId()
    {
        var id = RandomGenerator.Int64();
        id = id << 16;
        id = id + senderId;
        return id;
    }

    /// <summary>
    /// Gets the CLR type emitted by this source (wrapped <see cref="IRequestMessage{T}"/>).
    /// </summary>
    public Type SentMessageType => sourceHelper.SentMessageType;

    /// <summary>
    /// Gets the number of currently connected sinks (excluding collected weak references).
    /// </summary>
    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    /// <summary> Indicates whether there are no connected sinks. </summary>
    public bool NoConnectedSinks => sourceHelper.NotConnected;

    /// <summary>
    /// Gets the CLR type this sink consumes (wrapped <see cref="IResponseMessage{T}"/>).
    /// </summary>
    public Type ConsumedMessageType => typeof(IResponseMessage<RESP>);

    /// <summary>
    /// Connects this source to the specified sink.
    /// </summary>
    /// <param name="sink">Target sink to receive requests.</param>
    /// <param name="weakReference">True to hold a weak reference to the sink.</param>
    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {

        sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Connects to a bidirectional element and returns it as a source for chaining.
    /// </summary>
    /// <param name="sink">Target connection.</param>
    /// <param name="weakReference">True to hold a weak reference to the sink.</param>
    /// <returns>The connected element as <see cref="IMessageSource"/>.</returns>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Establishes a two-way connection with a replier (request/response).
    /// </summary>
    /// <param name="replier">The replier to connect to and back from.</param>
    /// <param name="weakReference">True to hold weak references.</param>
    public void ConnectToAndBack(IReplier replier, bool weakReference = false)
    {
        sourceHelper.ConnectTo(replier, weakReference);
        replier.ConnectTo(this, weakReference);
    }

    /// <summary>
    /// Disconnects all sinks from this source.
    /// </summary>
    public void DisconnectAll()
    {
        sourceHelper.DisconnectAll();
    }

    /// <summary>
    /// Disconnects a specific sink.
    /// </summary>
    /// <param name="sink">Sink to disconnect.</param>
    public void DisconnectFrom(IMessageSink sink)
    {
        sourceHelper.DisconnectFrom(sink);
    }

    /// <summary>
    /// Returns currently connected sinks (invalid weak references are pruned lazily).
    /// </summary>
    /// <returns>Array of connected sinks.</returns>
    public IMessageSink[] GetConnectedSinks()
    {
        return sourceHelper.GetConnectedSinks();
    }

    /// <summary>
    /// Receives a response message by reference.
    /// </summary>
    /// <typeparam name="M">Message type.</typeparam>
    /// <param name="message">Response message.</param>
    public void Post<M>(in M message)
    {
        PreHandleResponse(message);
    }

    private void PreHandleResponse<M>(M message)
    {
        if (message is IResponseMessage<RESP> responseMessage)
        {
            HandleResponse(responseMessage.Content, responseMessage.RequestId);
        }
    }

    /// <summary>
    /// Receives a response message by value.
    /// </summary>
    /// <typeparam name="M">Message type.</typeparam>
    /// <param name="message">Response message.</param>
    public void Post<M>(M message)
    {
        PreHandleResponse(message);
    }

    /// <summary>
    /// Receives a response message asynchronously.
    /// </summary>
    /// <typeparam name="M">Message type.</typeparam>
    /// <param name="message">Response message.</param>
    /// <returns>Completed task.</returns>
    public Task PostAsync<M>(M message)
    {
        PreHandleResponse(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks whether the specified sink is connected.
    /// </summary>
    /// <param name="sink">Sink to check.</param>
    /// <returns>True if connected; otherwise false.</returns>
    public bool IsConnected(IMessageSink sink)
    {
        return sourceHelper.IsConnected(sink);
    }

    /// <summary>
    /// Holds correlation data for a pending request.
    /// </summary>
    private readonly struct ResponseHandler
    {
        /// <summary>The time the request was issued.</summary>
        public readonly DateTime requestTime;
        /// <summary>Correlation id of the request.</summary>
        public readonly long requestId;
        /// <summary>Promise that completes with the response or is canceled on timeout.</summary>
        public readonly TaskCompletionSource<RESP> tcs;

        /// <summary>
        /// Creates a response handler with the provided request id and timestamp.
        /// </summary>
        /// <param name="requestId">Correlation id.</param>
        /// <param name="requestTime">Timestamp when the request was sent.</param>
        /// <param name="tcs">Task completion source that will be completed by a response.</param>
        public ResponseHandler(long requestId, DateTime requestTime, TaskCompletionSource<RESP> tcs)
        {
            this.requestId = requestId;
            this.requestTime = requestTime;
            this.tcs = tcs;
        }

        /// <summary>
        /// Factory that creates a handler with the current time and an async TCS.
        /// </summary>
        /// <param name="requestId">Correlation id.</param>
        /// <returns>Initialized <see cref="ResponseHandler"/>.</returns>
        public static ResponseHandler Create(long requestId, DateTime now) => new ResponseHandler(requestId, now, new TaskCompletionSource<RESP>(TaskCreationOptions.RunContinuationsAsynchronously));
    }
}
