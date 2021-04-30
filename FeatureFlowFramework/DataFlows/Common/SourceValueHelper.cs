using FeatureFlowFramework.Helpers.Synchronization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary> Can replace SourceHelper in situations where memory consumption and garbage must be minimized.
    /// WARNING: SourceValueHelper is a mutable struct and so there is a danger to cause severe bugs when not used
    /// properly (e.g. when boxing happens). Therefore, it doesn't implement IDataFlowSource.
    /// If you are unsure, better use the normal SourceHelper!<summary>
    public struct SourceValueHelper
    {
        [JsonIgnore]
        public DataFlowReference[] sinks;
        [JsonIgnore]
        MicroValueLock myLock;

        public IDataFlowSink[] GetConnectedSinks()
        {
            var currentSinks = this.sinks;

            if(currentSinks == null || currentSinks.Length == 0) return Array.Empty<IDataFlowSink>();
            else if(currentSinks.Length == 1)
            {
                if(currentSinks[0].TryGetTarget(out IDataFlowSink target))
                {
                    return new IDataFlowSink[] { target };
                }
                else
                {
                    RemoveInvalidReferences(1);
                    return Array.Empty<IDataFlowSink>();
                }
            }
            else
            {
                List<IDataFlowSink> resultList = null;
                int invalidReferences = 0;
                for(int i = currentSinks.Length - 1; i >= 0; i--)
                {
                    if(currentSinks[i].TryGetTarget(out IDataFlowSink target))
                    {
                        resultList = resultList ?? new List<IDataFlowSink>();
                        resultList.Add(target);
                    }
                    else
                    {
                        invalidReferences++;
                    }
                }
                if(invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
                return resultList?.ToArray() ?? Array.Empty<IDataFlowSink>();
            }
        }

        public int CountConnectedSinks
        {
            get
            {
                var currentSinks = this.sinks;

                if(currentSinks == null || currentSinks.Length == 0) return 0;
                else
                {
                    int invalidReferences = 0;
                    for(int i = currentSinks.Length - 1; i >= 0; i--)
                    {
                        if(!currentSinks[i].TryGetTarget(out IDataFlowSink target))
                        {
                            invalidReferences++;
                        }
                    }
                    if(invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
                    return this.sinks.Length;
                }
            }
        }

        public void Forward<M>(in M message)
        {
            if(this.sinks == null) return;

            var currentSinks = this.sinks;

            if(currentSinks.Length == 0) return;
            else if(currentSinks.Length == 1)
            {
                if(currentSinks[0].TryGetTarget(out IDataFlowSink target))
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
                for(int i = currentSinks.Length - 1; i >= 0; i--)
                {
                    if(currentSinks[i].TryGetTarget(out IDataFlowSink target))
                    {
                        target.Post(message);
                    }
                    else
                    {
                        invalidReferences++;
                    }
                }
                if(invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
            }
        }

        public Task ForwardAsync<M>(M message)
        {
            if(this.sinks == null) return Task.CompletedTask;

            var currentSinks = this.sinks;

            if(currentSinks.Length == 0) return Task.CompletedTask;
            else if(currentSinks.Length == 1)
            {
                if(currentSinks[0].TryGetTarget(out IDataFlowSink target))
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
                for(int i = currentSinks.Length - 1; i >= 0; i--)
                {
                    if(currentSinks[i].TryGetTarget(out IDataFlowSink target))
                    {
                        tasks[i] = target.PostAsync(message);
                    }
                    else
                    {
                        tasks[i] = Task.CompletedTask;
                        invalidReferences++;
                    }
                }
                if(invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
                return Task.WhenAll(tasks);
            }
        }

        private void RemoveInvalidReferences(int invalidReferences)
        {
            if(sinks == null) return;
            myLock.Enter(out var lockHandle);
            try
            {
                if (sinks.Length == invalidReferences) sinks = null;
                else
                {
                    DataFlowReference[] validSinks = new DataFlowReference[sinks.Length - invalidReferences];
                    int index = 0;
                    for (int i = sinks.Length - 1; i >= 0; i--)
                    {
                        if (sinks[i].TryGetTarget(out IDataFlowSink target))
                        {
                            validSinks[index++] = sinks[i];
                        }
                    }
                    sinks = validSinks;
                }
            }
            finally
            {
                myLock.Exit(lockHandle);
            }
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            if(sink == null) return;

            if(sinks == null)
            {
                myLock.Enter(out var lockHandle);
                try
                {
                    sinks = new DataFlowReference[] { new DataFlowReference(sink, weakReference) };
                }
                finally
                {
                    myLock.Exit(lockHandle);
                }
            }
            else
            {
                myLock.Enter(out var lockHandle);
                try
                {
                    DataFlowReference[] newSinks = new DataFlowReference[sinks.Length + 1];
                    sinks.CopyTo(newSinks, 0);                    
                    newSinks[newSinks.Length-1] = new DataFlowReference(sink, weakReference);
                    sinks = newSinks;                    
                }
                finally
                {
                    myLock.Exit(lockHandle);
                }
            }
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            ConnectTo(sink as IDataFlowSink);
            return sink;
        }

        public void DisconnectAll()
        {
            if (sinks == null) return;

            myLock.Enter(out var lockHandle);
            sinks = null;
            myLock.Exit(lockHandle);
        }
            

        public void DisconnectFrom(IDataFlowSink sink)
        {
            if(sinks == null) return;

            if (sinks.Length == 1)
            {
                if(!sinks[0].TryGetTarget(out IDataFlowSink target) || target == sink)
                {
                    RemoveInvalidReferences(1);
                }
            }
            else
            {
                int invalidReferences = 0;
                for (int i = sinks.Length - 1; i >= 0; i--)
                {
                    if (sinks[i].TryGetTarget(out IDataFlowSink target))
                    {
                        if (target == sink)
                        {
                            sinks[i] = new DataFlowReference();
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