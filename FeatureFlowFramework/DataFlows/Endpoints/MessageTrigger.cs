using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class MessageTrigger : IDataFlowSink, IAsyncWaitHandle
    {
        AsyncManualResetEvent mre = new AsyncManualResetEvent();
        bool autoReset;

        public MessageTrigger(bool autoReset)
        {
            this.autoReset = autoReset;
        }

        public void Trigger()
        {
            mre.Set();
        }

        public void Reset()
        {
            mre.Reset();
        }

        public bool IsTriggered(bool reset = false)
        {
            if(mre.IsSet)
            {
                if (reset) Reset();
                return true;
            }
            return false;
        }

        public Task WaitingTask => ((IAsyncWaitHandle)mre).WaitingTask;

        public void Post<M>(in M message)
        {
            mre.Set();
            if(autoReset) mre.Reset();
        }

        public Task PostAsync<M>(M message)
        {
            mre.Set();
            return Task.CompletedTask;
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return ((IAsyncWaitHandle)mre).TryConvertToWaitHandle(out waitHandle);
        }

        public bool Wait()
        {
            return ((IAsyncWaitHandle)mre).Wait();
        }

        public bool Wait(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)mre).Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).Wait(cancellationToken);
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).Wait(timeout, cancellationToken);
        }

        public Task<bool> WaitAsync()
        {
            return ((IAsyncWaitHandle)mre).WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)mre).WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).WaitAsync(timeout, cancellationToken);
        }

        public bool WouldWait()
        {
            return ((IAsyncWaitHandle)mre).WouldWait();
        }
    }
}
