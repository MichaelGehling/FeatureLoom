using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Extension methods for building and interacting with FeatureLoom message flows.
    /// Provides convenience helpers for processing, filtering, converting, splitting, responding,
    /// sending, and receiving with timeouts and cancellation tokens.
    /// </summary>
    public static class MessageFlowExtensions
    {
        /// <summary>
        /// Connects a processing endpoint that executes the provided action for messages of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The message type to process.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="action">The processing action invoked for each message.</param>
        public static void ProcessMessage<T>(this IMessageSource source, Action<T> action)
        {
            source.ConnectTo(new ProcessingEndpoint<T>(action));
        }

        /// <summary>
        /// Connects a processing endpoint that executes the provided action for messages of type <typeparamref name="T"/>, and returns the alternative message route.
        /// </summary>
        /// <typeparam name="T">The message type to process.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="action">The processing action invoked for each message. Return true to accept; return false to route to elseSource.</param>
        /// <param name="elseSource">The alternative source that receives messages when the processing returns false or receives messages of an unexpected type.</param>
        public static void ProcessMessage<T>(this IMessageSource source, Func<T, bool> action, out IMessageSource elseSource)
        {
            var sink = new ProcessingEndpoint<T>(action);
            elseSource = sink.Else;
            source.ConnectTo(sink);
        }

        /// <summary>
        /// Registers a responder: on incoming requests of type <typeparamref name="REQ"/>, uses <paramref name="handler"/> to compute a response of type <typeparamref name="RESP"/> and sends it with the same request id.
        /// </summary>
        /// <typeparam name="REQ">The incoming request payload type.</typeparam>
        /// <typeparam name="RESP">The outgoing response payload type.</typeparam>
        /// <param name="requester">The requester participating in the request/response flow.</param>
        /// <param name="handler">The handler that creates the response from the request payload.</param>
        public static void Respond<REQ, RESP>(this IRequester requester, Func<REQ, RESP> handler)
        {
            requester.ProcessMessage<IRequestMessage<REQ>>(request =>
            {
                var response = handler(request.Content);
                requester.Send(new ResponseMessage<RESP>(response, request.RequestId));
            });
        }

        /// <summary>
        /// Registers a conditional responder: only when <paramref name="condition"/> is true for the request payload, sends a response using <paramref name="handler"/> with the same request id.
        /// </summary>
        /// <typeparam name="REQ">The incoming request payload type.</typeparam>
        /// <typeparam name="RESP">The outgoing response payload type.</typeparam>
        /// <param name="requester">The requester participating in the request/response flow.</param>
        /// <param name="condition">Predicate to decide whether a response should be sent.</param>
        /// <param name="handler">The handler that creates the response from the request payload.</param>
        public static void RespondWhen<REQ, RESP>(this IRequester requester, Predicate<REQ> condition, Func<REQ, RESP> handler)
        {
            requester.ProcessMessage<IRequestMessage<REQ>>(request =>
            {
                if (!condition(request.Content)) return;

                var response = handler(request.Content);
                requester.Send(new ResponseMessage<RESP>(response, request.RequestId));
            });
        }

        /// <summary>
        /// Connects a converter that transforms incoming messages of type <typeparamref name="IN"/> to <typeparamref name="OUT"/> using <paramref name="convert"/>.
        /// </summary>
        /// <typeparam name="IN">Input message type.</typeparam>
        /// <typeparam name="OUT">Output message type.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="convert">Conversion function from <typeparamref name="IN"/> to <typeparamref name="OUT"/>.</param>
        /// <returns>The connected message source (the converter as a source).</returns>
        public static IMessageSource ConvertMessage<IN, OUT>(this IMessageSource source, Func<IN, OUT> convert)
        {
            return source.ConnectTo(new MessageConverter<IN, OUT>(convert));
        }

        /// <summary>
        /// Connects a splitter that splits an incoming message of type <typeparamref name="T"/> into multiple messages.
        /// </summary>
        /// <typeparam name="T">Input message type.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="split">Function that splits a message into an <see cref="ICollection"/> of items to forward.</param>
        /// <returns>The connected message source (the splitter as a source).</returns>
        public static IMessageSource SplitMessage<T>(this IMessageSource source, Func<T, ICollection> split)
        {
            return source.ConnectTo(new Splitter<T>(split));
        }

        /// <summary>
        /// Connects a filter that forwards only messages of type <typeparamref name="T"/> matching <paramref name="filter"/>.
        /// </summary>
        /// <typeparam name="T">Input message type to be filtered.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="filter">Predicate that determines whether to forward a message.</param>
        /// <returns>The connected message source (the filter as a source).</returns>
        public static IMessageSource FilterMessage<T>(this IMessageSource source, Predicate<T> filter)
        {
            return source.ConnectTo(new Filter<T>(filter));
        }

        /// <summary>
        /// Connects a filter that forwards only messages of type <typeparamref name="T"/> matching <paramref name="filter"/> and exposes an alternative route.
        /// </summary>
        /// <typeparam name="T">Input message type to be filtered.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="filter">Predicate that determines whether to forward a message.</param>
        /// <param name="elseSource">Alternative source that receives declined messages.</param>
        /// <returns>The connected message source (the filter as a source).</returns>
        public static IMessageSource FilterMessage<T>(this IMessageSource source, Predicate<T> filter, out IMessageSource elseSource)
        {
            var sink = new Filter<T>(filter);
            elseSource = sink.Else;
            return source.ConnectTo(sink);
        }

        /// <summary>
        /// Connects a batching connector that groups incoming messages of type <typeparamref name="T"/> into batches.
        /// </summary>
        /// <typeparam name="T">Input message type.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="maxBatchSize">Maximum number of items per batch. When reached, a batch is emitted immediately.</param>
        /// <param name="maxCollectionTime">Maximum time to collect before emitting the current batch (if not empty). 
        /// If <paramref name="maxCollectionTime"/> is zero or negative, the batch will only be flushed based on <paramref name="maxBatchSize"/>.</param>
        /// <param name="sendSingleMessagesAsArray">If true, even single items are sent as a T[] of length 1; if false, a single T is forwarded.</param>
        /// <returns>
        /// The connected message source (the batcher as a source). Note: output is untyped and may be either T or T[] depending on <paramref name="sendSingleMessagesAsArray"/>.
        /// </returns>
        public static IMessageSource BatchMessages<T>(
            this IMessageSource source,
            int maxBatchSize,
            TimeSpan maxCollectionTime,
            bool sendSingleMessagesAsArray = true)
        {
            if (maxBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxBatchSize));

            var buffer = new List<T>(Math.Max(4, maxBatchSize));

            void Flush(ISender s)
            {
                if (buffer.Count == 0) return;

                if (buffer.Count == 1 && !sendSingleMessagesAsArray)
                {
                    var item = buffer[0];
                    buffer.Clear();
                    s.Send(item);
                }
                else
                {
                    var arr = buffer.ToArray();
                    buffer.Clear();
                    s.Send(arr);
                }
            }

            // Use inactivity-based timer so the window is relative to the last received message.
            var useTimeout = maxCollectionTime > TimeSpan.Zero;

            var batcher = new Aggregator<T>(
                onMessage: (msg, s) =>
                {
                    buffer.Add(msg);
                    if (buffer.Count >= maxBatchSize)
                    {
                        Flush(s);
                    }
                },
                onTimeout: useTimeout ? new Action<ISender>(s => Flush(s)) : null,
                timeout: useTimeout ? maxCollectionTime : default,
                resetTimeoutOnMessage: false,
                autoLock: true
            );

            return source.ConnectTo(batcher);
        }

        /// <summary>
        /// Convenience wrapper to post a message to a sink.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="sink">The sink to post to.</param>
        /// <param name="message">The message to post.</param>
        public static void Send<T>(this IMessageSink sink, T message)
        {
            sink.Post(message);
        }

        /// <summary>
        /// Convenience wrapper to post a message to a sink by reference (useful for large structs).
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="sink">The sink to post to.</param>
        /// <param name="message">The message to post.</param>
        public static void Send<T>(this IMessageSink sink, in T message)
        {
            sink.Post(in message);
        }

        /// <summary>
        /// Convenience wrapper to post a message to a sink asynchronously.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="sink">The sink to post to.</param>
        /// <param name="message">The message to post.</param>
        /// <returns>A task that completes when the sink (and downstream) accepted the message asynchronously.</returns>
        public static Task SendAsync<T>(this IMessageSink sink, T message)
        {
            return sink.PostAsync(message);
        }

        /// <summary>
        /// Tries to receive a request message and extract its payload and request id.
        /// </summary>
        /// <typeparam name="T">Type of the request payload.</typeparam>
        /// <param name="receiver">Receiver of request messages.</param>
        /// <param name="message">Outputs the request payload if successful; otherwise default.</param>
        /// <param name="requestId">Outputs the request id if successful; otherwise default.</param>
        /// <returns>True if a request was received; otherwise false.</returns>
        public static bool TryReceiveRequest<T>(this IReceiver<IRequestMessage<T>> receiver, out T message, out long requestId)
        {
            if (receiver.TryReceive(out IRequestMessage<T> request))
            {
                requestId = request.RequestId;
                message = request.Content;
                return true;
            }
            else
            {
                message = default;
                requestId = default;
                return false;
            }
        }

        // Response helpers (untyped sender)

        /// <summary>
        /// Sends a response message constructed from payload and request id.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The sender to emit the response.</param>
        /// <param name="message">Response payload.</param>
        /// <param name="requestId">The correlating request id.</param>
        public static void SendResponse<T>(this ISender sender, T message, long requestId)
        {
            var response = new ResponseMessage<T>(message, requestId);
            sender.Send(response);
        }

        /// <summary>
        /// Sends an already wrapped response without modification.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The sender to emit the response.</param>
        /// <param name="response">The wrapped response.</param>
        public static void SendResponse<T>(this ISender sender, IResponseMessage<T> response)
        {
            sender.Send(response);
        }

        /// <summary>
        /// Sends a response asynchronously, constructed from payload and request id.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The sender to emit the response.</param>
        /// <param name="message">Response payload.</param>
        /// <param name="requestId">The correlating request id.</param>
        /// <returns>A task that completes when the message was sent asynchronously.</returns>
        public static Task SendResponseAsync<T>(this ISender sender, T message, long requestId)
        {
            var response = new ResponseMessage<T>(message, requestId);
            return sender.SendAsync(response);
        }

        /// <summary>
        /// Sends an already wrapped response asynchronously without modification.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The sender to emit the response.</param>
        /// <param name="response">The wrapped response.</param>
        /// <returns>A task that completes when the message was sent asynchronously.</returns>
        public static Task SendResponseAsync<T>(this ISender sender, IResponseMessage<T> response)
        {
            return sender.SendAsync(response);
        }

        // Response helpers (typed sender)

        /// <summary>
        /// Sends a response message constructed from payload and request id to a typed response sender.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The typed sender to emit the response.</param>
        /// <param name="message">Response payload.</param>
        /// <param name="requestId">The correlating request id.</param>
        public static void SendResponse<T>(this ISender<IResponseMessage<T>> sender, T message, long requestId)
        {
            var response = new ResponseMessage<T>(message, requestId);
            sender.Send(response);
        }

        /// <summary>
        /// Sends an already wrapped response without modification to a typed response sender.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The typed sender to emit the response.</param>
        /// <param name="response">The wrapped response.</param>
        public static void SendResponse<T>(this ISender<IResponseMessage<T>> sender, IResponseMessage<T> response)
        {
            sender.Send(response);
        }

        /// <summary>
        /// Sends a response message asynchronously, constructed from payload and request id, to a typed response sender.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The typed sender to emit the response.</param>
        /// <param name="message">Response payload.</param>
        /// <param name="requestId">The correlating request id.</param>
        /// <returns>A task that completes when the message was sent asynchronously.</returns>
        public static Task SendResponseAsync<T>(this ISender<IResponseMessage<T>> sender, T message, long requestId)
        {
            var response = new ResponseMessage<T>(message, requestId);
            return sender.SendAsync(response);
        }

        /// <summary>
        /// Sends an already wrapped response asynchronously without modification to a typed response sender.
        /// </summary>
        /// <typeparam name="T">Type of the response payload.</typeparam>
        /// <param name="sender">The typed sender to emit the response.</param>
        /// <param name="response">The wrapped response.</param>
        /// <returns>A task that completes when the message was sent asynchronously.</returns>
        public static Task SendResponseAsync<T>(this ISender<IResponseMessage<T>> sender, IResponseMessage<T> response)
        {
            return sender.SendAsync(response);
        }

        // Blocking/async receive helpers with cancellation

        /// <summary>
        /// Repeatedly waits (with cancellation) until a message can be received without blocking.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <param name="message">Outputs the received message if successful; otherwise default.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>True if a message was received; false if cancelled.</returns>
        public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, CancellationToken token)
        {
            while (!receiver.TryReceive(out message))
            {
                if (!receiver.WaitHandle.Wait(token)) return false;
            }
            return true;
        }

        /// <summary>
        /// Repeatedly waits (with cancellation) until a message is available to peek without removing it.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <param name="message">Outputs the next message if successful; otherwise default.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>True if a message was available to peek; false if cancelled.</returns>
        public static bool TryPeek<T>(this IReceiver<T> receiver, out T message, CancellationToken token)
        {
            while (!receiver.TryPeek(out message))
            {
                if (!receiver.WaitHandle.Wait(token)) return false;
            }
            return true;
        }

        /// <summary>
        /// Asynchronously waits (with cancellation) until a message can be received without blocking.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>
        /// A tuple where Item1 indicates success and Item2 is the received message (or default if cancelled).
        /// </returns>
        public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, CancellationToken token)
        {
            T message = default;
            while (!receiver.TryReceive(out message))
            {
                if (!(await receiver.WaitHandle.WaitAsync(token).ConfiguredAwait())) return (false, message);
            }
            return (true, message);
        }

        /// <summary>
        /// Asynchronously waits (with cancellation) until a message is available to peek without removing it.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>
        /// A tuple where Item1 indicates success and Item2 is the peeked message (or default if cancelled).
        /// </returns>
        public static async Task<(bool, T)> TryPeekAsync<T>(this IReceiver<T> receiver, CancellationToken token)
        {
            T message = default;
            while (!receiver.TryPeek(out message))
            {
                if (!(await receiver.WaitHandle.WaitAsync(token).ConfiguredAwait())) return (false, message);
            }
            return (true, message);
        }

        // Robust timeout loops (never return true without a message)

        /// <summary>
        /// Attempts to receive a message within the specified timeout.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <param name="message">Outputs the received message if successful; otherwise default.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>True if a message was received before the timeout; otherwise false.</returns>
        public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout)
        {
            if (receiver.TryReceive(out message)) return true;

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample))) return false;
                if (receiver.TryReceive(out message)) return true;
            }
            while (!timer.Elapsed());

            message = default;
            return false;
        }

        /// <summary>
        /// Attempts to peek a message within the specified timeout.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <param name="message">Outputs the peeked message if successful; otherwise default.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>True if a message was available before the timeout; otherwise false.</returns>
        public static bool TryPeek<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout)
        {
            if (receiver.TryPeek(out message)) return true;

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample))) return false;
                if (receiver.TryPeek(out message)) return true;
            }
            while (!timer.Elapsed());

            message = default;
            return false;
        }

        /// <summary>
        /// Asynchronously attempts to receive a message within the specified timeout.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>
        /// A tuple where Item1 indicates success and Item2 is the received message (or default if timed out).
        /// </returns>
        public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, TimeSpan timeout)
        {
            T message;
            if (receiver.TryReceive(out message)) return (true, message);

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample)).ConfiguredAwait())) return (false, default);
                if (receiver.TryReceive(out message)) return (true, message);
            }
            while (!timer.Elapsed());

            return (false, default);
        }

        /// <summary>
        /// Asynchronously attempts to peek a message within the specified timeout.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>
        /// A tuple where Item1 indicates success and Item2 is the peeked message (or default if timed out).
        /// </returns>
        public static async Task<(bool, T)> TryPeekAsync<T>(this IReceiver<T> receiver, TimeSpan timeout)
        {
            T message;
            if (receiver.TryPeek(out message)) return (true, message);

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample)).ConfiguredAwait())) return (false, default);
                if (receiver.TryPeek(out message)) return (true, message);
            }
            while (!timer.Elapsed());

            return (false, default);
        }

        /// <summary>
        /// Attempts to receive a message within the specified timeout or until cancelled.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <param name="message">Outputs the received message if successful; otherwise default.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>True if a message was received before the timeout or cancellation; otherwise false.</returns>
        public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout, CancellationToken token)
        {
            if (receiver.TryReceive(out message)) return true;

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample), token)) return false;
                if (receiver.TryReceive(out message)) return true;
            }
            while (!timer.Elapsed());

            message = default;
            return false;
        }

        /// <summary>
        /// Attempts to peek a message within the specified timeout or until cancelled.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <param name="message">Outputs the peeked message if successful; otherwise default.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>True if a message was available before the timeout or cancellation; otherwise false.</returns>
        public static bool TryPeek<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout, CancellationToken token)
        {
            if (receiver.TryPeek(out message)) return true;

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample), token)) return false;
                if (receiver.TryPeek(out message)) return true;
            }
            while (!timer.Elapsed());

            message = default;
            return false;
        }

        /// <summary>
        /// Asynchronously attempts to receive a message within the specified timeout or until cancelled.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>
        /// A tuple where Item1 indicates success and Item2 is the received message (or default if timed out or cancelled).
        /// </returns>
        public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, TimeSpan timeout, CancellationToken token)
        {
            T message;
            if (receiver.TryReceive(out message)) return (true, message);

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample), token).ConfiguredAwait())) return (false, default);
                if (receiver.TryReceive(out message)) return (true, message);
            }
            while (!timer.Elapsed());

            return (false, default);
        }

        /// <summary>
        /// Asynchronously attempts to peek a message within the specified timeout or until cancelled.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">Cancellation token to abort waiting.</param>
        /// <returns>
        /// A tuple where Item1 indicates success and Item2 is the peeked message (or default if timed out or cancelled).
        /// </returns>
        public static async Task<(bool, T)> TryPeekAsync<T>(this IReceiver<T> receiver, TimeSpan timeout, CancellationToken token)
        {
            T message;
            if (receiver.TryPeek(out message)) return (true, message);

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample), token).ConfiguredAwait())) return (false, default);
                if (receiver.TryPeek(out message)) return (true, message);
            }
            while (!timer.Elapsed());

            return (false, default);
        }

        /// <summary>
        /// Receives all currently available items by requesting a very large batch.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
        /// <remarks>
        /// Implementations may use a shared buffer if no explicit buffer is supplied internally.
        /// Do not hold onto the returned slice longer than necessary to avoid memory retention of the entire buffer.
        /// </remarks>
        public static ArraySegment<T> ReceiveAll<T>(this IReceiver<T> receiver)
        {
            return receiver.ReceiveMany(int.MaxValue, null);
        }

        /// <summary>
        /// Receives all currently available items into the provided buffer by requesting a very large batch.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to read from.</param>
        /// <param name="buffer">
        /// A reusable <see cref="SlicedBuffer{T}"/> to control slice lifetime and avoid retaining a shared buffer longer than necessary.
        /// After processing, consider freeing the slice (e.g., <c>buffer.FreeSlice(ref slice)</c>) if appropriate.
        /// </param>
        /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
        public static ArraySegment<T> ReceiveAll<T>(this IReceiver<T> receiver, SlicedBuffer<T> buffer)
        {
            return receiver.ReceiveMany(int.MaxValue, buffer);
        }

        /// <summary>
        /// Peeks all currently available items by requesting a very large batch without removing them.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
        /// <remarks>
        /// Implementations may use a shared buffer if no explicit buffer is supplied internally.
        /// Do not hold onto the returned slice longer than necessary to avoid memory retention of the entire buffer.
        /// </remarks>
        public static ArraySegment<T> PeekAll<T>(this IReceiver<T> receiver)
        {
            return receiver.PeekMany(int.MaxValue, null);
        }

        /// <summary>
        /// Peeks all currently available items into the provided buffer by requesting a very large batch, without removing them.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="receiver">The receiver to peek from.</param>
        /// <param name="buffer">
        /// A reusable <see cref="SlicedBuffer{T}"/> to control slice lifetime and avoid retaining a shared buffer longer than necessary.
        /// After processing, consider freeing the slice (e.g., <c>buffer.FreeSlice(ref slice)</c>) if appropriate.
        /// </param>
        /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
        public static ArraySegment<T> PeekAll<T>(this IReceiver<T> receiver, SlicedBuffer<T> buffer)
        {
            return receiver.PeekMany(int.MaxValue, buffer);
        }        
    }
}