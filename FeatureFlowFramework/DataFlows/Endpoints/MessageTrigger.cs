using FeatureFlowFramework.Helper;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class MessageTrigger : IDataFlowSink, IAsyncWaitHandle
    {
        private AsyncManualResetEvent mre = new AsyncManualResetEvent();
        private readonly Mode mode;

        public enum Mode
        {
            Default,
            InstantReset,
            Toggle
        }

        public MessageTrigger(Mode mode = Mode.Default)
        {
            this.mode = mode;
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
                if(reset) Reset();
                return true;
            }
            return false;
        }

        public Task WaitingTask => ((IAsyncWaitHandle)mre).WaitingTask;

        public void Post<M>(in M message)
        {
            HandleMessage(message);
        }

        public Task PostAsync<M>(M message)
        {
            HandleMessage(message);
            return Task.CompletedTask;
        }

        protected virtual void HandleMessage<M>(in M message)
        {
            if(mode == Mode.Toggle)
            {
                if(IsTriggered(false)) Reset();
                else Trigger();
            }
            else
            {
                mre.Set();
                if(mode == Mode.InstantReset) Reset();
            }
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