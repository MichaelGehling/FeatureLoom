using System;
using System.Collections;
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

    }
}