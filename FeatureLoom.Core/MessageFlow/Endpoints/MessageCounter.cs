using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class MessageCounter : IMessageSink
    {
        private volatile int counter;
        private FeatureLock myLock = new FeatureLock();
        private List<(int expectedCount, TaskCompletionSource<int> tcs)> waitings = new List<(int, TaskCompletionSource<int>)>();

        public int Counter
        {
            get 
            {
                using (myLock.LockReadOnly())
                {
                    return counter;
                }
            }
        }

        public Task<int> WaitForCountAsync(int numMessages)
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
        }

        public void Post<M>(M message)
        {
            Count();
        }

        public Task PostAsync<M>(M message)
        {
            Count();
            return Task.CompletedTask;
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
    }
}