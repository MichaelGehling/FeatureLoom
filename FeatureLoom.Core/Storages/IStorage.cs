using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{
    public interface IStorageReaderWriter : IStorageReader, IStorageWriter { }

    public interface IStorageReader
    {
        string Category { get; }

        Task<(bool, T)> TryReadAsync<T>(string uri);

        Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer);

        Task<(bool, string[])> TryListUrisAsync(string uriPattern = null);

        bool TrySubscribeForChangeNotifications(string uriPattern, IMessageSink<ChangeNotification> notificationSink);

        bool Exists(string uri);
    }

    public interface IStorageWriter
    {
        string Category { get; }

        Task<bool> TryWriteAsync<T>(string uri, T data);

        Task<bool> TryWriteAsync(string uri, Stream sourceStream);

        Task<bool> TryAppendAsync<T>(string uri, T data);

        Task<bool> TryAppendAsync(string uri, Stream sourceStream);

        Task<bool> TryDeleteAsync(string uri);

        bool Exists(string uri);
    }

    public interface IStorage
    {
        string Category { get; }

        bool Exists(string uri);

        Task<(bool, string[])> TryListUrisAsync(string uriPattern = null);
    }

    public interface IStorageObjectWriter<T> : IStorage
    {
        Task<bool> TryWriteAsync(string uri, T data);

        Task<bool> TryDeleteAsync(string uri);
    }

    public interface IStorageObjectReader<T> : IStorage
    {
        Task<(bool, T)> TryReadAsync(string uri);
    }

    public interface IStorageObjectLogger<T, K> : IStorage where K : IComparable<K>
    {
        Task<(bool, K)> TryLogAsync(string uri, T data);
    }
}