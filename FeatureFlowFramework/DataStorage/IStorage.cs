using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataStorage
{
    public interface IStorageReaderWriter : IStorageReader, IStorageWriter { }

    public interface IStorageReader
    {
        string Category { get; }

        bool TryRead<T>(string uri, out T data);

        Task<AsyncOutResult<bool, T>> TryReadAsync<T>(string uri);

        bool TryRead(string uri, Stream targetStream);

        Task<bool> TryReadAsync(string uri, Stream targetStream);

        bool TryListUris(out string[] uris, string pattern = null);

        Task<AsyncOutResult<bool, string[]>> TryListUrisAsync(string pattern = null);

        bool TrySubscribeForChangeNotifications(string uriPattern, IDataFlowSink notificationSink);
    }

    public static class StorageReaderExtensions
    {
        public static bool TrySubscribeForChangeUpdate<T>(this IStorageReader reader, string uriPattern, IDataFlowSink updateSink)
        {
            var converter = new Converter<ChangeNotification>(note => new ChangeUpdate<T>(note, reader));
            if(reader.TrySubscribeForChangeNotifications(uriPattern, converter))
            {
                converter.KeepAlive(updateSink);
                converter.ConnectTo(updateSink);
                return true;
            }
            return false;
        }
    }

    public readonly struct ChangeNotification
    {
        public readonly string category;
        public readonly string uri;
        public readonly UpdateEvent updateEvent;
        public readonly DateTime timestamp;

        public ChangeNotification(string category, string uri, UpdateEvent updateEvent, DateTime timestamp)
        {
            this.category = category;
            this.uri = uri;
            this.updateEvent = updateEvent;
            this.timestamp = timestamp;
        }
    }

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
            isValid = reader.TryRead<T>(uri, out item);
        }
    }

    public enum UpdateEvent
    {
        Created,
        Updated,
        Removed
    }

    public interface IStorageWriter
    {
        string Category { get; }

        bool TryWrite<T>(string uri, T data);

        Task<bool> TryWriteAsync<T>(string uri, T data);

        bool TryWrite(string uri, Stream sourceStream);

        Task<bool> TryWriteAsync(string uri, Stream sourceStream);

        bool TryAppend<T>(string uri, T data);
        Task<bool> TryAppendAsync<T>(string uri, T data);
        bool TryAppend(string uri, Stream sourceStream);
        Task<bool> TryAppendAsync(string uri, Stream sourceStream);
  
        bool TryDelete(string uri);

        Task<bool> TryDeleteAsync(string uri);
    }
}
