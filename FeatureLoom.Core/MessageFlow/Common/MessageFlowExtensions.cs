using FeatureLoom.Extensions;
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
        /// Connects a processing endpoint that executes the provided async action for messages of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The message type to process.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="action">The async processing action invoked for each message.</param>
        public static void ProcessMessage<T>(this IMessageSource source, Func<T, Task> action)
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
        /// <param name="forwardOtherMessages">True to forward messages that are not of type <typeparamref name="IN"/>.</param>
        /// <returns>The connected message source (the converter as a source).</returns>
        public static IMessageSource ConvertMessage<IN, OUT>(this IMessageSource source, Func<IN, OUT> convert, bool forwardOtherMessages = true)
        {
            return source.ConnectTo(new MessageConverter<IN, OUT>(convert, forwardOtherMessages));
        }

        /// <summary>
        /// Connects a splitter that splits an incoming message of type <typeparamref name="IN"/> into multiple messages.
        /// </summary>
        /// <typeparam name="IN">Input message type.</typeparam>
        /// <typeparam name="OUT">Output message type.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="split">Function that splits a message into a collection of items to forward.</param>
        /// <param name="forwardOtherMessages">True to forward messages that are not of type <typeparamref name="IN"/>.</param>
        /// <returns>The connected message source (the splitter as a source).</returns>
        public static IMessageSource SplitMessage<IN, OUT>(this IMessageSource source, Func<IN, IEnumerable<OUT>> split, bool forwardOtherMessages = true)
        {
            return source.ConnectTo(new Splitter<IN, OUT>(split, forwardOtherMessages));
        }

        /// <summary>
        /// Connects a filter that forwards only messages of type <typeparamref name="T"/> matching <paramref name="filter"/>.
        /// </summary>
        /// <typeparam name="T">Input message type to be filtered.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="filter">Predicate that determines whether to forward a message. If null all messages of type <typeparamref name="T"/> are forwarded.</param>
        /// <param name="forwardOtherMessages">True to forward messages that are not of type <typeparamref name="T"/>.</param>
        /// <returns>The connected message source (the filter as a source).</returns>
        public static IMessageSource FilterMessage<T>(this IMessageSource source, Predicate<T> filter = null, bool forwardOtherMessages = false)
        {
            return source.ConnectTo(new Filter<T>(filter, forwardOtherMessages));
        }

        /// <summary>
        /// Connects a filter that forwards only messages of type <typeparamref name="T"/> matching <paramref name="filter"/> and exposes an alternative route.
        /// </summary>
        /// <typeparam name="T">Input message type to be filtered.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="filter">Predicate that determines whether to forward a message.</param>
        /// <param name="elseSource">Alternative source that receives declined messages.</param>
        /// <param name="forwardOtherMessages">True to forward messages that are not of type <typeparamref name="T"/>.</param>
        /// <returns>The connected message source (the filter as a source).</returns>
        public static IMessageSource FilterMessage<T>(this IMessageSource source, Predicate<T> filter, out IMessageSource elseSource, bool forwardOtherMessages = true)
        {
            var sink = new Filter<T>(filter, forwardOtherMessages);
            elseSource = sink.Else;
            return source.ConnectTo(sink);
        }

        /// <summary>
        /// Connects a forwarder that delays forwarding of all messages by a fixed duration.
        /// </summary>
        /// <param name="source">The source to connect from.</param>
        /// <param name="delay">The fixed delay applied before forwarding. Must be non-negative.</param>
        /// <param name="delayPrecisely">True to use precise waiting APIs (higher CPU cost, higher accuracy).</param>
        /// <param name="blocking">
        /// True to block the caller until the delay elapsed. In this mode, asynchronous posts complete after the delay
        /// and forwarding finished (if sinks existed at call time).
        /// </param>
        /// <returns>The connected message source (the forwarder as a source) to continue the flow.</returns>
        /// <remarks>
        /// - The delay is always performed for consistency, regardless of whether sinks are currently connected.
        /// - Only messages for which at least one sink was connected at call time are forwarded after the delay.
        /// - In non-blocking mode, asynchronous posts return immediately after scheduling background work; completion does not represent forwarding.
        /// - See <see cref="DelayingForwarder"/> for detailed behavior and performance characteristics and cancellation support.
        /// </remarks>
        public static IMessageSource DelayMessage(this IMessageSource source, TimeSpan delay, bool delayPrecisely = false, bool blocking = false)
        {
            return source.ConnectTo(new DelayingForwarder(delay, delayPrecisely, blocking));
        }

        /// <summary>
        /// Connects a suppressor that filters out duplicate messages of type <typeparamref name="T"/> seen within a configurable time window.
        /// Messages of other types are forwarded unchanged.
        /// </summary>
        /// <typeparam name="T">Message type to check for duplicates.</typeparam>
        /// <param name="source">The source to connect from.</param>
        /// <param name="suppressionTimeWindow">
        /// Duration a seen message suppresses identical subsequent ones. Must be greater than zero.
        /// </param>
        /// <param name="isDuplicate">
        /// Optional predicate that decides whether two messages are duplicates. Defaults to <c>a.Equals(b)</c>.
        /// </param>
        /// <param name="preciseTime">
        /// True to use <see cref="AppTime.Now"/> for higher precision; otherwise uses <see cref="AppTime.CoarseNow"/> for lower overhead.
        /// </param>
        /// <returns>The connected message source (the suppressor as a source) to continue the flow.</returns>
        /// <remarks>
        /// Internally uses <see cref="DuplicateMessageSuppressor{T}"/>. Duplicate checks are an O(n) scan over the tracked window.
        /// Choose a short suppression window and/or a specialized predicate for better performance.
        /// </remarks>
        public static IMessageSource SuppressDuplicateMessages<T>(this IMessageSource source, TimeSpan suppressionTimeWindow, Func<T, T, bool> isDuplicate = null, bool preciseTime = false)
        {
            return source.ConnectTo(new DuplicateMessageSuppressor<T>(suppressionTimeWindow, isDuplicate, preciseTime));
        }

        /// <summary>
        /// Filters messages <see cref="ITopicMessage"/> from the source based on the specified topic or topic pattern.
        /// </summary>
        /// <remarks>If the <paramref name="topicFilter"/> contains wildcard characters (<c>*</c> or
        /// <c>?</c>),  the filter will use pattern matching to determine whether a message's topic matches the filter. 
        /// Otherwise, an exact match is required.</remarks>
        /// <param name="source">The source of messages to filter.</param>
        /// <param name="unwrap">If true ITopicMessage the contained message will be unwrapped before forwarded, otherwise the ITopicMessage will be forwarded</param>
        /// <param name="topicFilter">The topic or wildcard pattern to filter messages by. Wildcard characters such as  <c>*</c> and <c>?</c> can
        /// be used to match multiple topics.</param>
        /// <param name="forwardOtherMessages">A value indicating whether messages that are not of type <see cref="ITopicMessage"/> should be forwarded  
        /// to the next connected component. The default is false.</param>
        /// <returns>A new <see cref="IMessageSource"/> that emits only the messages matching the specified  topic or pattern.</returns>
        public static IMessageSource FilterByTopic(this IMessageSource source, string topicFilter, bool unwrap = false, bool forwardOtherMessages = false)
        {
            bool isPattern = topicFilter.Contains("*") || topicFilter.Contains("?");
            IMessageFlowConnection filter = new Filter<ITopicMessage>(msg => isPattern ? msg.Topic.MatchesWildcardPattern(topicFilter) : msg.Topic == topicFilter, 
                                                   forwardOtherMessages);
            source.ConnectTo(filter);
            return unwrap ? filter.UnwrapMessage() : filter;
        }

        /// <summary>
        /// Unwraps a message from the specified <see cref="IMessageSource"/> and forwards it to a new message source.
        /// </summary>
        /// <remarks>This method processes the message from the provided <paramref name="source"/> by
        /// unwrapping it using an <see cref="IMessageWrapper"/> and forwarding the unwrapped content to a new message
        /// source. The returned <see cref="IMessageSource"/> can then be used to access the unwrapped
        /// message.</remarks>
        /// <param name="source">The source of the message to be unwrapped. Cannot be <see langword="null"/>.</param>
        /// <returns>A new <see cref="IMessageSource"/> containing the unwrapped message.</returns>
        public static IMessageSource UnwrapMessage(this IMessageSource source)
        {
            var typeFilter = new Filter<IMessageWrapper>(null, false);
            var unwrapper = new Aggregator<IMessageWrapper>(
                onMessage: (msg, sender) => msg.UnwrapAndSend(sender),
                autoLock: false
            );            
            var forwarder = new Forwarder();

            source.ConnectTo(typeFilter).ConnectTo(unwrapper).ConnectTo(forwarder);
            typeFilter.Else.ConnectTo(forwarder);            

            return forwarder;
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

    }
}