using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class ConditionalTrigger<T, R> : IMessageSink, IAsyncWaitHandle
    {
        MessageTrigger internalTrigger = new MessageTrigger();
        private readonly Predicate<T> triggerCondition;
        private readonly Predicate<R> resetCondition;

        public Task WaitingTask => ((IAsyncWaitHandle)internalTrigger).WaitingTask;

        public ConditionalTrigger(Predicate<T> triggerCondition, Predicate<R> resetCondition = null)
        {
            this.triggerCondition = triggerCondition;
            this.resetCondition = resetCondition;
        }

        public bool IsTriggered(bool reset = false)
        {
            return internalTrigger.IsTriggered(reset);
        }

        public void Post<M>(in M message)
        {
            HandleMessage(message);
        }

        public void Post<M>(M message)
        {
            HandleMessage(message);
        }

        public Task PostAsync<M>(M message)
        {
            HandleMessage(message);
            return Task.CompletedTask;
        }

        void HandleMessage<M>(M message)
        {
            if (message is T trigger && (triggerCondition == null || triggerCondition(trigger))) internalTrigger.Trigger();
            if (message is R reset && resetCondition != null && resetCondition(reset)) internalTrigger.Reset();
        }

        public Task<bool> WaitAsync()
        {
            return ((IAsyncWaitHandle)internalTrigger).WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)internalTrigger).WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)internalTrigger).WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)internalTrigger).WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            return ((IAsyncWaitHandle)internalTrigger).Wait();
        }

        public bool Wait(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)internalTrigger).Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)internalTrigger).Wait(cancellationToken);
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)internalTrigger).Wait(timeout, cancellationToken);
        }

        public bool WouldWait()
        {
            return ((IAsyncWaitHandle)internalTrigger).WouldWait();
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return ((IAsyncWaitHandle)internalTrigger).TryConvertToWaitHandle(out waitHandle);
        }

        
    }
}