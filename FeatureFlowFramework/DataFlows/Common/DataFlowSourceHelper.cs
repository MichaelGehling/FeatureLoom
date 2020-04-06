using FeatureFlowFramework.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary> Helps implementing IDataFlowSource and should be used wherever IDataFlowSource is
    /// implemented. It is thread safe, but doesn't need any lock while sending, so it will never
    /// block if used concurrently. Anyway, changing the list of connected sinks
    /// (connecting/disconnecting) uses a lock and might block. Sinks are stored as weak references
    /// and will not be kept from being garbage-collected, so it is not necessary to disconnect
    /// sinks that are not needed any more. To avoid that a DataFlowSource with active connections
    /// is garbage collected, the helper will maintain a strong reference in a static hashset that
    /// will be removed when all connected sinks are disconnected. <summary>
    public class DataFlowSourceHelper : IDataFlowSource
    {
        [JsonIgnore]
        volatile public List<WeakReference<IDataFlowSink>> sinks;

        private readonly Action<IDataFlowSink> onConnection;
        private readonly Action<IDataFlowSink> onDisconnection;

        FeatureLock myLock = new FeatureLock(FeatureLock.ONLY_SPIN_WAIT);

        public DataFlowSourceHelper(Action<IDataFlowSink> onConnection = null, Action<IDataFlowSink> onDisconnection = null)
        {
            this.onConnection = onConnection;
            this.onDisconnection = onDisconnection;
        }

        public DataFlowSourceHelper()
        {
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            var currentSinks = this.sinks;

            if(currentSinks == null || currentSinks.Count == 0) return Array.Empty<IDataFlowSink>();
            else if(currentSinks.Count == 1)
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
                for(int i = currentSinks.Count - 1; i >= 0; i--)
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

                if(currentSinks == null || currentSinks.Count == 0) return 0;
                else
                {
                    int invalidReferences = 0;
                    for(int i = currentSinks.Count - 1; i >= 0; i--)
                    {
                        if(!currentSinks[i].TryGetTarget(out IDataFlowSink target))
                        {
                            invalidReferences++;
                        }
                    }
                    if(invalidReferences > 0) RemoveInvalidReferences(invalidReferences);
                    return this.sinks.Count;
                }
            }
        }

        public void Forward<M>(in M message)
        {
            if(message == null || this.sinks == null) return;

            var currentSinks = this.sinks;

            if(currentSinks.Count == 0) return;
            else if(currentSinks.Count == 1)
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
                for(int i = currentSinks.Count - 1; i >= 0; i--)
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
            if(message == null || this.sinks == null) return Task.CompletedTask;

            var currentSinks = this.sinks;

            if(currentSinks.Count == 0) return Task.CompletedTask;
            else if(currentSinks.Count == 1)
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
                Task[] tasks = new Task[currentSinks.Count];
                for(int i = currentSinks.Count - 1; i >= 0; i--)
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
            using(myLock.ForWriting())
            {
                List<WeakReference<IDataFlowSink>> validSinks = new List<WeakReference<IDataFlowSink>>(sinks.Count - invalidReferences);
                for(int i = sinks.Count - 1; i >= 0; i--)
                {
                    if(sinks[i].TryGetTarget(out IDataFlowSink target))
                    {
                        validSinks.Add(sinks[i]);
                    }
                }
                sinks = validSinks.Count > 0 ? validSinks : null;
            }
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            if(sinks == null)
            {
                using (myLock.ForWriting())
                {
                    List<WeakReference<IDataFlowSink>> newSinks = new List<WeakReference<IDataFlowSink>>(1);
                    newSinks.Add(new WeakReference<IDataFlowSink>(sink));
                    sinks = newSinks;
                    onConnection?.Invoke(sink);
                }
            }
            else
            {
                using (myLock.ForWriting())
                {
                    List<WeakReference<IDataFlowSink>> newSinks = new List<WeakReference<IDataFlowSink>>(sinks.Count + 1);
                    newSinks.AddRange(sinks);
                    newSinks.Add(new WeakReference<IDataFlowSink>(sink));
                    sinks = newSinks;
                    onConnection?.Invoke(sink);
                }
            }
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            ConnectTo(sink as IDataFlowSink);
            return sink;
        }

        public void DisconnectAll()
        {
            var currentSinks = this.sinks;
            foreach(var sinkRef in currentSinks.EmptyIfNull())
            {
                if(sinkRef.TryGetTarget(out IDataFlowSink sink)) onDisconnection?.Invoke(sink);
            }

            if(sinks == null || sinks.Count == 0) return;
            using (myLock.ForWriting())
            {
                sinks = null;
            }
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            onDisconnection?.Invoke(sink);
            if(sinks == null) return;
            using (myLock.ForWriting())
            {
                if(sinks.Count == 1)
                {
                    if(sinks[0].TryGetTarget(out IDataFlowSink target) && target == sink)
                    {
                        sinks = null;
                    }
                }
                else
                {
                    List<WeakReference<IDataFlowSink>> remainingSinks = new List<WeakReference<IDataFlowSink>>(sinks.Count - 1);
                    for(int i = sinks.Count - 1; i >= 0; i--)
                    {
                        if(sinks[i].TryGetTarget(out IDataFlowSink target) && target != sink)
                        {
                            remainingSinks.Add(sinks[i]);
                        }
                    }
                    sinks = remainingSinks;
                }
            }
        }
    }
}