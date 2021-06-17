using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Diagnostics
{
    public class CountingForwarder : IMessageSink, IMessageSource, IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper;

        private volatile int counter;
        private FeatureLock myLock = new FeatureLock();
        private List<(int expectedCount, TaskCompletionSource<int> tcs)> waitings = new List<(int, TaskCompletionSource<int>)>();

        public int Counter
        {
            get { using (myLock.LockReadOnly()) return counter; }
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Task<int> WaitForAsync(int numMessages)
        {
            using (myLock.Lock())
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
            sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
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
            using (myLock.Lock())
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

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
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
    }
}