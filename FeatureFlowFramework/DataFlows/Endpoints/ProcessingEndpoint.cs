using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     Messages put to the processing endpoint are processed in the passed action. The
    ///     processing function returns true if successful and false if not, then the message will
    ///     be forwarded to 'Else'.
    ///     It is thread-safe as long as the passed action is also
    ///     thread-safe. If the action is not thread safe, the synchronizationLock parameter in
    ///     the constructor can be used to define a lock object.
    ///     Avoid long running processing actions or run them in a task to avoid
    ///     blocking the sender.
    /// </summary>
    /// <typeparam name="T"> </typeparam>
    public class ProcessingEndpoint<T> : IDataFlowSink, IAlternativeDataFlow
    {
        private readonly Func<T, bool> processing;
        private readonly Func<T, Task<bool>> processingAsync;
        private DataFlowSourceHelper alternativeSendingHelper = null;
        private readonly object syncLock;

        public ProcessingEndpoint(Func<T, bool> processing, object synchronizationLock = null)
        {
            this.processing = processing;
            syncLock = synchronizationLock;
        }

        public ProcessingEndpoint(Action<T> processing, object synchronizationLock = null)
        {
            this.processing = t =>
            {
                processing(t);
                return true;
            };
            syncLock = synchronizationLock;
        }

        public ProcessingEndpoint(Func<T, Task<bool>> processingAsync)
        {
            this.processingAsync = processingAsync;
        }

        public IDataFlowSource Else
        {
            get
            {
                if (alternativeSendingHelper == null) alternativeSendingHelper = new DataFlowSourceHelper();
                return alternativeSendingHelper;
            }
        }

        public void Post<M>(in M message)
        {
            if (message is T msgT)
            {
                if (processing != null)
                {
                    if (syncLock == null)
                    {
                        if (!processing(msgT))
                        {
                            alternativeSendingHelper?.Forward(message);
                        }
                    }
                    else
                    {
                        bool result;
                        lock (syncLock)
                        {
                            result = processing(msgT);
                        }
                        if (!result) alternativeSendingHelper?.Forward(message);
                    }
                }
                else
                {
                    if (!processingAsync(msgT).Result)
                    {
                        alternativeSendingHelper?.Forward(message);
                    }
                }
            }
            else alternativeSendingHelper?.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            if (message is T msgT)
            {
                if (processing != null)
                {
                    if (syncLock == null)
                    {
                        if (!processing(msgT))
                        {
                            alternativeSendingHelper?.Forward(message);
                        }
                    }
                    else
                    {
                        bool result;
                        lock (syncLock)
                        {
                            result = processing(msgT);
                        }
                        if (!result) alternativeSendingHelper?.Forward(message);
                    }
                }
                else
                {
                    if (!await processingAsync(msgT))
                    {
                        await alternativeSendingHelper?.ForwardAsync(message);
                    }
                }
            }
            else await alternativeSendingHelper?.ForwardAsync(message);
        }
    }
}