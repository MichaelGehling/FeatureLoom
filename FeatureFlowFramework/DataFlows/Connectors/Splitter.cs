using System;
using System.Collections;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class Splitter<T> : IDataFlowConnection, IDataFlowSink, IDataFlowSource, IAlternativeDataFlow
    {
        private DataFlowSourceHelper sender = new DataFlowSourceHelper();
        private DataFlowSourceHelper alternativeSender = new DataFlowSourceHelper();
        private readonly Func<T, ICollection> split;

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
                    sender.Forward(msg);
                    alternative = false;
                }
            }

            if (alternative) alternativeSender.Forward(message);
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
                        tasks[i++] = sender.ForwardAsync(msg);
                    }
                    return Task.WhenAll(tasks);
                }
            }
            return alternativeSender?.ForwardAsync(message) ?? Task.CompletedTask;
        }

        public int CountConnectedSinks => ((IDataFlowSource)sender).CountConnectedSinks;

        public IDataFlowSource Else => alternativeSender;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sender).ConnectTo(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sender).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sender).GetConnectedSinks();
        }
    }
}