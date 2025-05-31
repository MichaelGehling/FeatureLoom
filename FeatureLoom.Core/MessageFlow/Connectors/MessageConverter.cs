using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    ///     Messages from a message put to the converter are processed in a converter function and
    ///     forwarded to connected sinks. Messages that doesn't match the input type are forwarded as they are. It
    ///     is thread-safe as long as the provided function is also thread-safe. Avoid long running
    ///     functions to avoid blocking the sender
    /// </summary>
    /// <typeparam name="I"> The input type for the converter function </typeparam>
    /// <typeparam name="O"> The output type for the converter function </typeparam>
    public sealed class MessageConverter<I, O> : IMessageFlowConnection<I, O>
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private readonly Func<I, O> convertFunc;
        private bool forwardOtherMessages;

        public Type SentMessageType => typeof(O);
        public Type ConsumedMessageType => typeof(I);

        public MessageConverter(Func<I, O> convertFunc, bool forwardOtherMessages = true)
        {
            this.convertFunc = convertFunc;
            this.forwardOtherMessages = forwardOtherMessages;
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void Post<M>(in M message)
        {
            if (message is I msgT)
            {
                O output = convertFunc(msgT);                
                // TODO: It would be good to check if O is a readonly struct and in that case use "sourceHelper.Forward(in output);"
                sourceHelper.Forward(output);
            }
            else if (forwardOtherMessages) sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (message is I msgT)
            {
                O output = convertFunc(msgT);
                // TODO: It would be good to check if O is a readonly struct and in that case use "sourceHelper.Forward(in output);"
                sourceHelper.Forward(output);
            }
            else if (forwardOtherMessages) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is I msgT)
            {
                return sourceHelper.ForwardAsync(convertFunc(msgT));
            }
            else if (forwardOtherMessages) return sourceHelper.ForwardAsync(message);
            return Task.CompletedTask;
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}