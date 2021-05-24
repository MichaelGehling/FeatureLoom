using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
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
    public class ProcessingEndpoint<T> : IMessageSink<T>, IAlternativeMessageSource
    {
        private readonly Func<T, bool> processing;
        private readonly Func<T, Task<bool>> processingAsync;
        private LazyValue<SourceHelper> alternativeSendingHelper;

        //TODO: Better switch to FeatureLock?
        private readonly object syncLock;
        
        public Type ConsumedMessageType => typeof(T);

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

        public IMessageSource Else => alternativeSendingHelper.Obj;

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
                            alternativeSendingHelper.ObjIfExists?.Forward(in message);
                        }
                    }
                    else
                    {
                        bool result;
                        lock (syncLock)
                        {
                            result = processing(msgT);
                        }
                        if (!result) alternativeSendingHelper.ObjIfExists?.Forward(in message);
                    }
                }
                else
                {
                    if (!processingAsync(msgT).WaitFor())
                    {
                        alternativeSendingHelper.ObjIfExists?.Forward(in message);
                    }
                }
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (message is T msgT)
            {
                if (processing != null)
                {
                    if (syncLock == null)
                    {
                        if (!processing(msgT))
                        {
                            alternativeSendingHelper.ObjIfExists?.Forward(message);
                        }
                    }
                    else
                    {
                        bool result;
                        lock (syncLock)
                        {
                            result = processing(msgT);
                        }
                        if (!result) alternativeSendingHelper.ObjIfExists?.Forward(message);
                    }
                }
                else
                {
                    if (!processingAsync(msgT).WaitFor())
                    {
                        alternativeSendingHelper.ObjIfExists?.Forward(message);
                    }
                }
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
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
                            alternativeSendingHelper.ObjIfExists?.Forward(message);
                        }
                    }
                    else
                    {
                        bool result;
                        lock (syncLock)
                        {
                            result = processing(msgT);
                        }
                        if (!result) alternativeSendingHelper.ObjIfExists?.Forward(message);
                    }
                }
                else
                {
                    if (!await processingAsync(msgT))
                    {
                        await alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
                    }
                }
            }
            else await alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
        }
    }
}