using FeatureLoom.Synchronization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary> Can replace SourceHelper in situations where memory consumption and garbage must be minimized.
    /// WARNING: SourceValueHelper is a mutable struct and so there is a danger to cause severe bugs when not used
    /// properly (e.g. when boxing happens). Therefore, it doesn't implement IMessageSource.
    /// If you are unsure, better use the normal SourceHelper!<summary>
    public struct TypedSourceValueHelper<T>
    {
        [JsonIgnore]
        public MessageSinkRef[] sinks;

        [JsonIgnore]
        private MicroValueLock myLock;

        public Type SentMessageType => typeof(T);

        public IMessageSink[] GetConnectedSinks()
        {
            var currentSinks = this.sinks;

            if (currentSinks == null || currentSinks.Length == 0) return Array.Empty<IMessageSink>();
            else if (currentSinks.Length == 1)
            {
                if (currentSinks[0].TryGetTarget(out IMessageSink target))
                {
                    return new IMessageSink[] { target };
                }
                else
                {
                    RemoveInvalidReferences(1);
                    return Array.Empty<IMessageSink>();
                }
            }
            else
            {
                List<IMessageSink> resultList = null;
                int invalidReferences = 0;
                for (int i = currentSinks.Length - 1; i >= 0; i--)
                {
                    if (currentSinks[i].TryGetTarget(out IMessageSink target))
                    {
                        resultList = resultList ?? new List<IMessageSink>();
                        resultList.Add(target);
                    }
                    else
                    {
                        invalidReferences++;
                    }
                }
                if (invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
                return resultList?.ToArray() ?? Array.Empty<IMessageSink>();
            }
        }

        public int CountConnectedSinks
        {
            get
            {
                var currentSinks = this.sinks;

                if (currentSinks == null || currentSinks.Length == 0) return 0;
                else
                {
                    int invalidReferences = 0;
                    for (int i = currentSinks.Length - 1; i >= 0; i--)
                    {
                        if (!currentSinks[i].TryGetTarget(out IMessageSink target))
                        {
                            invalidReferences++;
                        }
                    }
                    if (invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
                    return this.sinks.Length;
                }
            }
        }

        public void Forward<M>(in M message)
        {
            if (this.sinks == null) return;

            var currentSinks = this.sinks;

            if (currentSinks.Length == 0) return;
            else if (currentSinks.Length == 1)
            {
                if (currentSinks[0].TryGetTarget(out IMessageSink target))
                {
                    target.Post(in message);
                }
                else
                {
                    RemoveInvalidReferences(1);
                }
            }
            else
            {
                int invalidReferences = 0;
                for (int i = currentSinks.Length - 1; i >= 0; i--)
                {
                    if (currentSinks[i].TryGetTarget(out IMessageSink target))
                    {
                        target.Post(in message);
                    }
                    else
                    {
                        invalidReferences++;
                    }
                }
                if (invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
            }
        }

        public void Forward<M>(M message)
        {
            if (this.sinks == null) return;

            var currentSinks = this.sinks;

            if (currentSinks.Length == 0) return;
            else if (currentSinks.Length == 1)
            {
                if (currentSinks[0].TryGetTarget(out IMessageSink target))
                {
                    target.Post(message);
                }
                else
                {
                    RemoveInvalidReferences(1);
                }
            }
            else
            {
                int invalidReferences = 0;
                for (int i = currentSinks.Length - 1; i >= 0; i--)
                {
                    if (currentSinks[i].TryGetTarget(out IMessageSink target))
                    {
                        target.Post(message);
                    }
                    else
                    {
                        invalidReferences++;
                    }
                }
                if (invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
            }
        }

        public Task ForwardAsync<M>(M message)
        {
            if (this.sinks == null) return Task.CompletedTask;

            var currentSinks = this.sinks;

            if (currentSinks.Length == 0) return Task.CompletedTask;
            else if (currentSinks.Length == 1)
            {
                if (currentSinks[0].TryGetTarget(out IMessageSink target))
                {
                    return target.PostAsync(message);
                }
                else
                {
                    RemoveInvalidReferences(1);
                    return Task.CompletedTask;
                }
            }
            else
            {
                int invalidReferences = 0;
                Task[] tasks = new Task[currentSinks.Length];
                for (int i = currentSinks.Length - 1; i >= 0; i--)
                {
                    if (currentSinks[i].TryGetTarget(out IMessageSink target))
                    {
                        tasks[i] = target.PostAsync(message);
                    }
                    else
                    {
                        tasks[i] = Task.CompletedTask;
                        invalidReferences++;
                    }
                }
                if (invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
                return Task.WhenAll(tasks);
            }
        }

        private void RemoveInvalidReferences(int invalidReferences)
        {
            if (sinks == null) return;
            myLock.Enter();
            try
            {
                if (sinks.Length == invalidReferences) sinks = null;
                else
                {
                    MessageSinkRef[] validSinks = new MessageSinkRef[sinks.Length - invalidReferences];
                    int index = 0;
                    for (int i = sinks.Length - 1; i >= 0; i--)
                    {
                        if (sinks[i].TryGetTarget(out IMessageSink target))
                        {
                            validSinks[index++] = sinks[i];
                        }
                    }
                    sinks = validSinks;
                }
            }
            finally
            {
                myLock.Exit();
            }
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            if (sink == null) return;
            if (sink is ITypedMessageSink typedSink && !typedSink.ConsumedMessageType.IsAssignableFrom(typeof(T))) throw new Exception("It is not allowed to connect a TypedMessageSource to a TypedMessageSink if the type of the source cannot be assigned to the sink type!");

            if (sinks == null)
            {
                myLock.Enter();
                try
                {
                    sinks = new MessageSinkRef[] { new MessageSinkRef(sink, weakReference) };
                }
                finally
                {
                    myLock.Exit();
                }
            }
            else
            {
                myLock.Enter();
                try
                {
                    MessageSinkRef[] newSinks = new MessageSinkRef[sinks.Length + 1];
                    sinks.CopyTo(newSinks, 0);
                    newSinks[newSinks.Length - 1] = new MessageSinkRef(sink, weakReference);
                    sinks = newSinks;
                }
                finally
                {
                    myLock.Exit();
                }
            }
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            ConnectTo(sink as IMessageSink);
            return sink;
        }

        public void DisconnectAll()
        {
            if (sinks == null) return;

            myLock.Enter();
            sinks = null;
            myLock.Exit();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            if (sinks == null) return;

            if (sinks.Length == 1)
            {
                if (!sinks[0].TryGetTarget(out IMessageSink target) || target == sink)
                {
                    RemoveInvalidReferences(1);
                }
            }
            else
            {
                int invalidReferences = 0;
                for (int i = sinks.Length - 1; i >= 0; i--)
                {
                    if (sinks[i].TryGetTarget(out IMessageSink target))
                    {
                        if (target == sink)
                        {
                            sinks[i] = new MessageSinkRef();
                            invalidReferences++;
                        }
                    }
                    else invalidReferences++;
                }
                if (invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
            }
        }
    }
}