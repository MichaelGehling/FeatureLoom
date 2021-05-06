using FeatureLoom.DataFlows;
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

        Task<AsyncOut<bool, T>> TryReadAsync<T>(string uri);

        Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer);

        Task<AsyncOut<bool, string[]>> TryListUrisAsync(string pattern = null);

        bool TrySubscribeForChangeNotifications(string uriPattern, IDataFlowSink notificationSink);

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
}