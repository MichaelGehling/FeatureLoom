using FeatureLoom.MessageFlow;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{
    public class StorageReaderWriterUriDecorator : IStorageReaderWriter, IStorageWriter, IStorageReader
    {        
        StorageReaderUriDecorator readerDecorator;
        StorageWriterUriDecorator writerDecorator;

        public StorageReaderWriterUriDecorator(string category, IStorageReaderWriter readerWriter, Func<string, string> transformUri, Func<string, string> retransformUri)
        {
            readerDecorator = new StorageReaderUriDecorator(category, readerWriter, transformUri, retransformUri);
            writerDecorator = new StorageWriterUriDecorator(category, readerWriter, transformUri);
        }

        public StorageReaderWriterUriDecorator(string category, IStorageReaderWriter readerWriter, bool extendForStorage, string uriExtensionPattern)
        {
            readerDecorator = new StorageReaderUriDecorator(category, readerWriter, extendForStorage, uriExtensionPattern);
            writerDecorator = new StorageWriterUriDecorator(category, readerWriter, extendForStorage, uriExtensionPattern);
        }

        public string Category => ((IStorageReader)readerDecorator).Category;

        public bool Exists(string uri)
        {
            return ((IStorageReader)readerDecorator).Exists(uri);
        }

        public Task<bool> TryAppendAsync<T>(string uri, T data)
        {
            return ((IStorageWriter)writerDecorator).TryAppendAsync(uri, data);
        }

        public Task<bool> TryAppendAsync(string uri, Stream sourceStream)
        {
            return ((IStorageWriter)writerDecorator).TryAppendAsync(uri, sourceStream);
        }

        public Task<bool> TryDeleteAsync(string uri)
        {
            return ((IStorageWriter)writerDecorator).TryDeleteAsync(uri);
        }

        public Task<(bool, string[])> TryListUrisAsync(string uriPattern = null)
        {
            return ((IStorageReader)readerDecorator).TryListUrisAsync(uriPattern);
        }

        public Task<(bool, T)> TryReadAsync<T>(string uri)
        {
            return ((IStorageReader)readerDecorator).TryReadAsync<T>(uri);
        }

        public Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer)
        {
            return ((IStorageReader)readerDecorator).TryReadAsync(uri, consumer);
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IMessageSink<ChangeNotification> notificationSink)
        {
            return ((IStorageReader)readerDecorator).TrySubscribeForChangeNotifications(uriPattern, notificationSink);
        }

        public Task<bool> TryWriteAsync<T>(string uri, T data)
        {
            return ((IStorageWriter)writerDecorator).TryWriteAsync(uri, data);
        }

        public Task<bool> TryWriteAsync(string uri, Stream sourceStream)
        {
            return ((IStorageWriter)writerDecorator).TryWriteAsync(uri, sourceStream);
        }
    }
}