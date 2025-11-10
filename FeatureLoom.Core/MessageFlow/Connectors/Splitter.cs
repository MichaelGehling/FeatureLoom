using System;
using System.Collections;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class Splitter<T> : IMessageFlowConnection, IMessageSink<T>
    {
        private SourceValueHelper sourceHelper;
        private readonly Func<T, ICollection> split;
        
        public Type ConsumedMessageType => typeof(T);

        public Splitter(Func<T, ICollection> split)
        {
            this.split = split;
        }

        public void Post<M>(in M message)
        {
            bool alternative = true;
            if (message is T tMsg)
            {
                var output = split(tMsg);
                foreach (var msg in output)
                {
                    sourceHelper.Forward(in msg);
                    alternative = false;
                }
            }

            if (alternative) sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
        {
            bool alternative = true;
            if (message is T tMsg)
            {
                var output = split(tMsg);
                foreach (var msg in output)
                {
                    sourceHelper.Forward(msg);
                    alternative = false;
                }
            }

            if (alternative) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T tMsg)
            {
                var output = split(tMsg);
                if (output.Count > 0)
                {
                    Task[] tasks = new Task[output.Count];
                    int i = 0;
                    foreach (var msg in output)
                    {
                        tasks[i++] = sourceHelper.ForwardAsync(msg);
                    }
                    return Task.WhenAll(tasks);
                }
            }
            return sourceHelper.ForwardAsync(message);
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NotConnected;

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
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