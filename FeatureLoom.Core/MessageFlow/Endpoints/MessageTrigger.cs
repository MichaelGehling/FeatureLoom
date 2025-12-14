using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// A message sink that exposes a waitable trigger using an asynchronous manual reset mechanism.
    /// Depending on the <see cref="Mode"/>, incoming messages either set, pulse, or toggle the trigger state.
    /// </summary>
    public sealed class MessageTrigger : IMessageSink, IAsyncWaitHandle
    {
        private readonly AsyncManualResetEvent mre = new AsyncManualResetEvent();
        private readonly Mode mode;

        /// <summary>
        /// Defines how incoming messages affect the trigger state.
        /// </summary>
        public enum Mode
        {
            /// <summary>
            /// Messages set the trigger and keep it set until <see cref="Reset"/> is called manually.
            /// </summary>
            ManualReset,
            /// <summary>
            /// Messages cause a pulse (release current waiters) without leaving the trigger set for future waiters.
            /// Semantically similar to Set+Reset (a one-shot notification).
            /// </summary>
            InstantReset,
            /// <summary>
            /// Messages toggle the trigger state: if set, it is reset; if reset, it is set.
            /// Note: This is not atomic across threads; concurrent calls may race.
            /// </summary>
            Toggle
        }

        /// <summary>
        /// Creates a new <see cref="MessageTrigger"/> with the specified handling <see cref="Mode"/>.
        /// </summary>
        /// <param name="mode">Determines how incoming messages affect the trigger state.</param>
        public MessageTrigger(Mode mode = Mode.ManualReset)
        {
            this.mode = mode;
        }

        /// <summary>
        /// Sets the trigger, releasing current and future waiters (until reset).
        /// </summary>
        public void Trigger()
        {
            mre.Set();
        }

        /// <summary>
        /// Resets the trigger, causing future waiters to block until triggered again.
        /// </summary>
        public void Reset()
        {
            mre.Reset();
        }

        /// <summary>
        /// Indicates whether the trigger is currently set and optionally resets it.
        /// </summary>
        /// <param name="reset">If true and the trigger is set, it will be reset before returning.</param>
        /// <returns>True if the trigger is currently set; otherwise false.</returns>
        public bool IsTriggered(bool reset = false)
        {
            if (mre.IsSet)
            {
                if (reset) Reset();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a task that completes when the trigger becomes set.
        /// </summary>
        public Task WaitingTask => ((IAsyncWaitHandle)mre).WaitingTask;

        /// <summary>
        /// Posts a message by reference and updates the trigger based on the configured <see cref="Mode"/>.
        /// </summary>
        /// <typeparam name="M">The message type.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(in M message)
        {
            HandleMessage();
        }

        /// <summary>
        /// Posts a message and updates the trigger based on the configured <see cref="Mode"/>.
        /// </summary>
        /// <typeparam name="M">The message type.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(M message)
        {
            HandleMessage();
        }

        /// <summary>
        /// Asynchronously posts a message and updates the trigger based on the configured <see cref="Mode"/>.
        /// </summary>
        /// <typeparam name="M">The message type.</typeparam>
        /// <param name="message">The message to post.</param>
        /// <returns>A completed task.</returns>
        public Task PostAsync<M>(M message)
        {
            HandleMessage();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Applies the configured <see cref="Mode"/> to update the trigger state for an incoming message.
        /// </summary>
        void HandleMessage()
        {
            if (mode == Mode.ManualReset)
            {
                mre.Set();
            }
            else if (mode == Mode.InstantReset)
            {
                // Pulse releases current waiters and immediately resets for late arrivals.
                mre.PulseAll();
            }
            else
            {
                // Toggle is non-atomic across threads; concurrent posts may race.
                if (mre.IsSet) mre.Reset();
                else mre.Set();
            }
        }

        /// <summary>
        /// Attempts to convert to a synchronous <see cref="WaitHandle"/> for interoperability.
        /// </summary>
        /// <param name="waitHandle">The resulting wait handle if conversion succeeds.</param>
        /// <returns>True if conversion succeeded; otherwise false.</returns>
        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return ((IAsyncWaitHandle)mre).TryConvertToWaitHandle(out waitHandle);
        }

        /// <summary>
        /// Blocks until the trigger is set.
        /// </summary>
        /// <returns>True if the wait completed because the trigger was set.</returns>
        public bool Wait()
        {
            return ((IAsyncWaitHandle)mre).Wait();
        }

        /// <summary>
        /// Blocks until the trigger is set or the timeout elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>True if the trigger was set within the timeout; otherwise false.</returns>
        public bool Wait(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)mre).Wait(timeout);
        }

        /// <summary>
        /// Blocks until the trigger is set or cancellation is requested.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        /// <returns>True if the trigger was set before cancellation; otherwise false.</returns>
        public bool Wait(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).Wait(cancellationToken);
        }

        /// <summary>
        /// Blocks until the trigger is set, the timeout elapses, or cancellation is requested.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        /// <returns>True if the trigger was set before timeout/cancellation; otherwise false.</returns>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).Wait(timeout, cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits until the trigger is set.
        /// </summary>
        /// <returns>A task that completes with true when the trigger is set.</returns>
        public Task<bool> WaitAsync()
        {
            return ((IAsyncWaitHandle)mre).WaitAsync();
        }

        /// <summary>
        /// Asynchronously waits until the trigger is set or the timeout elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <returns>A task that completes with true if set within the timeout; otherwise false.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)mre).WaitAsync(timeout);
        }

        /// <summary>
        /// Asynchronously waits until the trigger is set or cancellation is requested.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        /// <returns>A task that completes with true if set before cancellation; otherwise false.</returns>
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits until the trigger is set, the timeout elapses, or cancellation is requested.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        /// <returns>A task that completes with true if set before timeout/cancellation; otherwise false.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)mre).WaitAsync(timeout, cancellationToken);
        }

        /// <summary>
        /// Indicates if a wait would currently block (i.e., the trigger is not set).
        /// </summary>
        /// <returns>True if a wait would block; otherwise false.</returns>
        public bool WouldWait()
        {
            return ((IAsyncWaitHandle)mre).WouldWait();
        }
    }
}