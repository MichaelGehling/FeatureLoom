using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.Test
{
    public class CountingForwarder : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        private DataFlowSourceHelper sourceHelper = new DataFlowSourceHelper();

        private volatile int counter;
        private object locker = new object();
        private List<(int expectedCount, TaskCompletionSource<int> tcs)> waitings = new List<(int, TaskCompletionSource<int>)>();

        public int Counter
        {
            get { lock (locker) return counter; }
        }

        public int CountConnectedSinks => ((IDataFlowSource)sourceHelper).CountConnectedSinks;

        public Task<int> WaitFor(int numMessages)
        {
            lock (locker)
            {
                var currentCounter = counter;
                TaskCompletionSource<int> waitingTaskSource = new TaskCompletionSource<int>();
                if (currentCounter >= numMessages) waitingTaskSource.SetResult(currentCounter);
                else
                {
                    waitings.Add((numMessages, waitingTaskSource));
                }
                return waitingTaskSource.Task;
            }
        }

        public void Post<M>(in M message)
        {
            Count();
            sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            Count();
            return sourceHelper.ForwardAsync(message);
        }

        private void Count()
        {
            lock (locker)
            {
                counter++;
                for (int i = waitings.Count - 1; i >= 0; i--)
                {
                    if (waitings[i].expectedCount <= counter)
                    {
                        waitings[i].tcs.SetResult(counter);
                        waitings.RemoveAt(i);
                    }
                }
            }
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sourceHelper).DisconnectAll();
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sourceHelper).GetConnectedSinks();
        }
    }
}