using FeatureLoom.Synchronization;
using System;

namespace FeatureLoom.Storages
{
    public readonly struct ChangeUpdate<T>
    {
        public readonly string category;
        public readonly string uri;
        public readonly UpdateEvent updateEvent;
        public readonly DateTime timestamp;
        public readonly bool isValid;
        public readonly T item;

        public ChangeUpdate(ChangeNotification notification, IStorageReader reader = null)
        {
            this.category = notification.category;
            this.uri = notification.uri;
            this.updateEvent = notification.updateEvent;
            this.timestamp = notification.timestamp;
            reader = reader ?? Storage.GetReader(category);
            isValid = reader.TryReadAsync<T>(uri).WaitFor(out item);
        }
    }
}