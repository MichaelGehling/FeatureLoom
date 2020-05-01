using FeatureFlowFramework.DataFlows;
using System;

namespace FeatureFlowFramework.Helper
{
    public class SharedData<T> : ISharedData
    {
        private FeatureLock myLock = new FeatureLock();
        private T value;
        private Sender updateSender;

        public SharedData(T value)
        {
            this.value = value;
        }

        public IDataFlowSource UpdateNotifications
        {
            get
            {
                if(updateSender == null) updateSender = new Sender();
                return updateSender;
            }
        }

        public Type ValueType => value?.GetType() ?? typeof(T);

        private void PublishUpdate(long updateOriginatorId)
        {
            updateSender?.Send(new SharedDataUpdateNotification(this, updateOriginatorId));
        }

        public WriteAccess GetWriteAccess(long updateOriginatorId = -1) => new WriteAccess(myLock.ForWriting(), this, updateOriginatorId);

        public ReadAccess GetReadAccess() => new ReadAccess(myLock.ForReading(), this);

        public void WithWriteAccess(Action<WriteAccess> writeAction, long updateOriginatorId = -1)
        {
            using(var writer = this.GetWriteAccess(updateOriginatorId)) writeAction(writer);
        }

        public void WithReadAccess(Action<ReadAccess> readAction)
        {
            using(var reader = this.GetReadAccess()) readAction(reader);
        }

        public struct WriteAccess : IDisposable
        {
            private FeatureLock.WriteLock myLock;
            private SharedData<T> shared;
            private bool publish;
            private readonly long updateOriginatorId;

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
                if(publish) shared?.PublishUpdate(updateOriginatorId);
                shared = null;
            }
        }

        public struct ReadAccess : IDisposable
        {
            private FeatureLock.ReadLock myLock;
            private SharedData<T> shared;

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