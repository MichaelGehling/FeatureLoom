using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public static class MessageFlowExtensions
    {
        public static void ProcessMessage<T>(this IMessageSource source, Action<T> action)
        {
            source.ConnectTo(new ProcessingEndpoint<T>(action));
        }

        public static void ProcessMessage<T>(this IMessageSource source, Action<T> action, out IMessageSource elseSource)
        {
            var sink = new ProcessingEndpoint<T>(action);
            elseSource = sink.Else;
            source.ConnectTo(sink);            
        }

        public static IMessageSource ConvertMessage<IN, OUT>(this IMessageSource source, Func<IN,OUT> convert)
        {
            return source.ConnectTo(new MessageConverter<IN,OUT>(convert));
        }        

        public static IMessageSource SplitMessage<T>(this IMessageSource source, Func<T, ICollection> split)
        {
            return source.ConnectTo(new Splitter<T>(split));
        }

        public static IMessageSource FilterMessage<T>(this IMessageSource source, Predicate<T> filter)
        {
            return source.ConnectTo(new Filter<T>(filter));
        }

        public static IMessageSource FilterMessage<T>(this IMessageSource source, Predicate<T> filter, out IMessageSource elseSource)
        {
            var sink = new Filter<T>(filter);
            elseSource = sink.Else;
            return source.ConnectTo(sink);
        }        

        public static void Send<T>(this IMessageSink sink, T message)
        {
            sink.Post(message);
        }

        public static void Send<T>(this IMessageSink sink, in T message)
        {
            sink.Post(in message);
        }

        public static Task SendAsync<T>(this IMessageSink sink, T message)
        {
            return sink.PostAsync(message);
        }

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

        public static void SendResponse<T>(this ISender sender, T message, long requestId)
        {
            if (message is IResponseMessage<T> response)
            {
                response.RequestId = requestId;
            }
            else
            {
                response = new ResponseMessage<T>(message, requestId);
            }
            sender.Send(response);
        }

        public static void SendResponse<T>(this ISender<IResponseMessage<T>> sender, T message, long requestId)
        {
            if (message is IResponseMessage<T> response)
            {
                response.RequestId = requestId;
            }
            else
            {
                response = new ResponseMessage<T>(message, requestId);
            }
            sender.Send(response);
        }

        public static Task SendResponseAsync<T>(this ISender sender, T message, long requestId)
        {
            if (message is IResponseMessage<T> response)
            {
                response.RequestId = requestId;
            }
            else
            {
                response = new ResponseMessage<T>(message, requestId);
            }
            return sender.SendAsync(response);
        }

        public static Task SendResponseAsync<T>(this ISender<IResponseMessage<T>> sender, T message, long requestId)
        {
            if (message is IResponseMessage<T> response)
            {
                response.RequestId = requestId;
            }
            else
            {
                response = new ResponseMessage<T>(message, requestId);
            }
            return sender.SendAsync(response);
        }

        public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, CancellationToken token)
        {
            while(!receiver.TryReceive(out message))
            {
                if (!receiver.WaitHandle.Wait(token)) return false;
            }
            return true;
        }

        public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, CancellationToken token)
        {
            T message = default;
            while (!receiver.TryReceive(out message))
            {
                if (!(await receiver.WaitHandle.WaitAsync(token))) return (false, message);
            }
            return (true, message);
        }

        public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout)
        {
            if (receiver.TryReceive(out message)) return true;

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample))) return false;
            }
            while (!receiver.TryReceive(out message) && !timer.Elapsed());

            return true;
        }

        public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, TimeSpan timeout)
        {
            T message = default;
            if (receiver.TryReceive(out message)) return (true, message);

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample)))) return (false, message);
            }
            while (!receiver.TryReceive(out message) && !timer.Elapsed());

            return (true, message);
        }

        public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout, CancellationToken token)
        {
            if (!receiver.TryReceive(out message)) return true;

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample), token)) return false;
            }
            while (!receiver.TryReceive(out message) && !timer.Elapsed());

            return true;
        }

        public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, TimeSpan timeout, CancellationToken token)
        {
            T message = default;
            if (!receiver.TryReceive(out message)) return (true, message);

            TimeFrame timer = new TimeFrame(timeout);
            do
            {
                if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample), token))) return (false, message);
            }
            while (!receiver.TryReceive(out message) && !timer.Elapsed());

            return (true, message);
        }

    }
}