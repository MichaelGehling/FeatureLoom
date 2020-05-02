﻿using FeatureFlowFramework.DataFlows;
using System;

namespace FeatureFlowFramework.Helper
{
    /// <summary>
    /// Shared data can be used in multiple parallel contexts to store and share data.
    /// The sharedData variable has to grant read or write access, before the data can be accessed.
    /// Additionally, it is possible to listen to update notifications, so one context can react 
    /// on data changes made by another. When requesting write access an originatorId can be set, so
    /// it is possible to identifiy update notifications from specific originators, e.g. to filter
    /// own changes.
    /// 
    /// Usage:
    /// using(var access = sharedData.GetWriteAccess())
    /// {
    ///     access.SetValue(someValue)
    /// }
    /// 
    /// IMPORTANT: The access-variable must be used with a using block, so it will be disposed afterwards.
    /// If the WriteAccess is not disposed, the access to the shared data is permanently blocked!
    /// </summary>
    /// <typeparam name="T"></typeparam>
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

        /// <summary>
        /// Provides read and write access to the shared data.
        /// The WriteAccess must be used in a using block, so it will be disposed afterwards.
        /// IMPORTANT: If the WriteAccess is not disposed, the access to the shared data is permanently blocked!
        /// By default the SharedData will send a notification when the WriteAccess is disposed,
        /// which can be suppressed by calling SuppressPublishUpdate() inside the using block;
        /// 
        /// That problem could be reduced by using a class instead of a struct (a finalizer could call the dispose),
        /// but creating a new object each time a variable is accessed would be quite costly on the GC.
        /// </summary>
        public struct WriteAccess : IDisposable
        {
            private FeatureLock.WriteLock myLock;
            private SharedData<T> shared;
            private bool publish;
            private readonly long updateOriginatorId;

            public T Value
            {
                get => shared.value;
                //set => shared.value = value; //Not possible with a struct as a using-variable :(
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

        /// <summary>
        /// Provides only read access to the shared data.
        /// The ReadAccess must be used in a using block, so it will be disposed afterwards.
        /// IMPORTANT: If the ReadAccess is not disposed, the access to the shared data is permanently blocked!
        ///
        /// That problem could be reduced by using a class instead of a struct (a finalizer could call the dispose),
        /// but creating a new object each time a variable is accessed would be quite costly on the GC.
        /// </summary>
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