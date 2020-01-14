using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataStorage
{
    public interface IStorageReaderWriter : IStorageReader, IStorageWriter { }

    public interface IStorageReader
    {
        string Category { get; }

        bool TryRead<T>(string uri, out T data);

        Task<AsyncOutResult<bool, T>> TryReadAsync<T>(string uri);

        bool TryRead(string uri, Action<Stream> consumer);

        Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer);

        bool TryListUris(out string[] uris, string pattern = null);

        Task<AsyncOutResult<bool, string[]>> TryListUrisAsync(string pattern = null);

        bool TrySubscribeForChangeNotifications(string uriPattern, IDataFlowSink notificationSink);

        bool Exists(string uri);
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

        bool Exists(string uri);
    }
}