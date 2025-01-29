using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary> Can replace SourceHelper in situations where memory consumption and garbage must be minimized.
    /// WARNING: SourceValueHelper is a mutable struct and so there is a danger to cause severe bugs when not used
    /// properly (e.g. when boxing happens). Therefore, it doesn't implement IMessageSource.
    /// If you are unsure, better use the normal SourceHelper!<summary>
    public struct SourceValueHelper
    {
        [JsonIgnore]
        public MessageSinkRef[] sinks;

        [JsonIgnore]
        private MicroValueLock changeLock;

        public IMessageSink[] GetConnectedSinks()
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return Array.Empty<IMessageSink>();

            bool anyInvalid = false;
            List<IMessageSink> resultList = new List<IMessageSink>(currentSinks.Length);

            for (int i = 0; i < currentSinks.Length; i++)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target)) resultList.Add(target);                    
                else anyInvalid = true;                    
            }

            if (anyInvalid) LockAndRemoveInvalidReferences();
            return resultList.ToArray();
        }

        public int CountConnectedSinks
        {
            get
            {
                var currentSinks = this.sinks;
                if (currentSinks == null) return 0;

                int count = 0;
                for (int i = 0; i < currentSinks.Length; i++)
                {
                    if (currentSinks[i].IsValid) count++;
                }
                if (count != currentSinks.Length) LockAndRemoveInvalidReferences();

                return count;
            }
        }

        public void Forward<M>(in M message)
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return;

            bool anyInvalid = false;
            for (int i = 0; i < currentSinks.Length; i++)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target)) target.Post(in message);
                else anyInvalid = true;
            }
            if (anyInvalid) LockAndRemoveInvalidReferences();
        }

        public void Forward<M>(M message)
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return;

            bool anyInvalid = false;
            for (int i = 0; i < currentSinks.Length; i++)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target)) target.Post(message);
                else anyInvalid = true;
            }
            if (anyInvalid) LockAndRemoveInvalidReferences();
        }

        public Task ForwardAsync<M>(M message)
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return Task.CompletedTask;

            if (currentSinks.Length == 1)
            {
                if (currentSinks[0].TryGetTarget(out IMessageSink target))
                {
                    return target.PostAsync(message);
                }
                else
                {
                    LockAndRemoveInvalidReferences();
                    return Task.CompletedTask;
                }
            }
            else return MultipleForwardAsync(message, currentSinks);
        }

        private async Task MultipleForwardAsync<M>(M message, MessageSinkRef[] currentSinks)
        {
            bool anyInvalid = false;
            for (int i = currentSinks.Length - 1; i >= 0; i--)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target)) await target.PostAsync(message).ConfigureAwait(false);                
                else anyInvalid = true;                
            }
            if (anyInvalid) LockAndRemoveInvalidReferences();            
        }

        private void LockAndRemoveInvalidReferences()
        {
            if (sinks == null) return;
            changeLock.Enter();
            try
            {
                RemoveInvalidReferences();
            }
            finally
            {
                changeLock.Exit();
            }
        }

        // NOTE: Lock must already be acquired!
        private void RemoveInvalidReferences()
        {
            if (sinks == null) return;

            if (sinks.Length == 1)
            {
                if (sinks[0].IsValid) return;
                sinks = null;
                return;
            }

            var changeList = new List<MessageSinkRef>();
            for (int i = 0; i < sinks.Length; i++)
            {
                if (sinks[i].IsValid) changeList.Add(sinks[i]);                
            }            
            sinks = changeList.Count == 0 ? null : changeList.ToArray();
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            if (sink == null) return;

            changeLock.Enter();
            try
            {
                if (sinks == null)
                {                
                    sinks = new MessageSinkRef[] { new MessageSinkRef(sink, weakReference) };                
                }
                else
                {
                    MessageSinkRef[] newSinks = new MessageSinkRef[sinks.Length + 1];
                    sinks.CopyTo(newSinks, 0);
                    newSinks[newSinks.Length - 1] = new MessageSinkRef(sink, weakReference);
                    sinks = newSinks;
                }
            }
            finally
            {
                changeLock.Exit();
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

            changeLock.Enter();
            sinks = null;
            changeLock.Exit();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            if (sinks == null) return;

            changeLock.Enter();
            try
            {
                if (sinks.Length == 1)
                {
                    if (!sinks[0].TryGetTarget(out IMessageSink target) || target == sink)
                    {
                        sinks = null;
                    }
                }
                else
                {
                    int? index = null;
                    for (int i = 0; i < sinks.Length; i++)
                    {
                        if (sinks[i].TryGetTarget(out IMessageSink target) && target == sink)
                        {
                            index = i;
                            break;
                        }
                    }
                    if (!index.HasValue) return;

                    MessageSinkRef[] newSinks = new MessageSinkRef[sinks.Length - 1];
                    Array.Copy(sinks, 0, newSinks, 0, index.Value);
                    Array.Copy(sinks, index.Value + 1, newSinks, index.Value, newSinks.Length - index.Value);
                    sinks = newSinks;
                }
            }
            finally
            {
                changeLock.Exit();
            }
        }
    }
}