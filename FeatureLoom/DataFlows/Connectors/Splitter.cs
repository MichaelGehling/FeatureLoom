using System;
using System.Collections;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    public class Splitter<T> : IDataFlowConnection, IDataFlowSink<T>
    {
        private SourceValueHelper sourceHelper;
        private readonly Func<T, ICollection> split;

        public Splitter(Func<T, ICollection> split)
        {
            this.split = split;
        }

        public void Post<M>(in M message)
        {
            bool alternative = true;
            if(message is T tMsg)
            {
                var output = split(tMsg);
                foreach(var msg in output)
                {
                    sourceHelper.Forward(msg);
                    alternative = false;
                }
            }

            if(alternative) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is T tMsg)
            {
                var output = split(tMsg);
                if(output.Count > 0)
                {
                    Task[] tasks = new Task[output.Count];
                    int i = 0;
                    foreach(var msg in output)
                    {
                        tasks[i++] = sourceHelper.ForwardAsync(msg);
                    }
                    return Task.WhenAll(tasks);
                }
            }
            return sourceHelper.ForwardAsync(message);
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }
}