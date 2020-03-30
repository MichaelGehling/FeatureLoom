using FeatureFlowFramework.DataFlows;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public class SharedData<T> : ISharedData
    {
        FeatureLock myLock = new FeatureLock();
        T value;
        Sender updateSender;
        string name;

        public SharedData(T value, string name = null)
        {
            this.value = value;
            this.name = name;
        }

        public IDataFlowSource UpdateNotifications
        {
            get
            {
                if (updateSender == null) updateSender = new Sender();
                return updateSender;
            }
        }

        public string Name => name;

        public Type ValueType => value?.GetType() ?? typeof(T);

        void PublishUpdate(long updateOriginatorId)
        {
            updateSender?.Send(new SharedDataUpdateNotification(this, updateOriginatorId));
        }        

        public WriteAccess GetWriteAccess(long updateOriginatorId = -1) => new WriteAccess(myLock.ForWriting(), this, updateOriginatorId);
        public ReadAccess GetReadAccess() => new ReadAccess(myLock.ForReading(), this);

        public void WithWriteAccess(Action<WriteAccess> writeAction, long updateOriginatorId = -1)
        {
            using (var writer = this.GetWriteAccess(updateOriginatorId)) writeAction(writer);
        }

        public void WithReadAccess(Action<ReadAccess> readAction)
        {
            using (var reader = this.GetReadAccess()) readAction(reader);
        }        

        public struct WriteAccess : IDisposable
        {
            FeatureLock.WriteLock myLock;
            SharedData<T> shared;
            bool publish;
            long updateOriginatorId;

            public T Value
            {
                get => shared.value;
                //set => shared.value = value;
            }

            public void SetValue(T newValue) => shared.value = newValue;
            public void SuppressPublishUpdate() => publish = false;

            public WriteAccess(FeatureLock.WriteLock myLock, SharedData<T> shared, long updateOriginatorId)
            {
                this.myLock = myLock;
                this.shared = shared;
                this.publish = true;
                this.updateOriginatorId = updateOriginatorId;
            }            

            public void Dispose()
            {
                myLock.Dispose();
                if (publish) shared?.PublishUpdate(updateOriginatorId);
                shared = null;
            }
        }

        public struct ReadAccess : IDisposable
        {
            FeatureLock.ReadLock myLock;
            SharedData<T> shared;

            public T Value
            {
                get => shared.value;
            }

            public ReadAccess(FeatureLock.ReadLock myLock, SharedData<T> shared)
            {
                this.myLock = myLock;
                this.shared = shared;
            }

            public void Dispose()
            {
                myLock.Dispose();
                shared = null;
            }
        }                
    }

    public interface ISharedData
    {
        string Name { get; }
        Type ValueType { get; }
        IDataFlowSource UpdateNotifications { get; }
    }

    public readonly struct SharedDataUpdateNotification
    {
        public readonly ISharedData sharedData;
        public readonly long originatorId;

        public SharedDataUpdateNotification(ISharedData sharedData, long originatorId)
        {
            this.sharedData = sharedData;
            this.originatorId = originatorId;
        }
    }
}
