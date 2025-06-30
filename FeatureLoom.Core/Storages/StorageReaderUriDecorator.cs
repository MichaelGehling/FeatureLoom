using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{

    public class StorageReaderUriDecorator : IStorageReader
    {
        string category;
        IStorageReader reader;
        Func<string, string> transformUri;
        Func<string, string> retransformUri;

        public StorageReaderUriDecorator(string category, IStorageReader reader, Func<string, string> transformUri, Func<string, string> retransformUri)
        {
            this.category = category;
            this.reader = reader;
            this.transformUri = transformUri;
            this.retransformUri = retransformUri;
        }

        public StorageReaderUriDecorator(string category, IStorageReader reader, bool extendForStorage, string uriExtensionPattern)
        {
            this.category = category;
            this.reader = reader;

            string pre = uriExtensionPattern.Substring(0, "", "{uri}", out int restart);
            string post = uriExtensionPattern.Substring(restart, "{uri}", "", out _);
            var extractor = new PatternExtractor(uriExtensionPattern);
            if (extendForStorage)
            {
                transformUri = uri => $"{pre}{uri ?? "*"}{post}";
                retransformUri = extendedUri => extendedUri == null || extendedUri == "*" ? "*" :
                                                extractor.TryExtract(extendedUri, out string uri) ? uri : null;
            }
            else
            {
                transformUri = extendedUri => extendedUri == null || extendedUri == "*" ? "*" :
                                              extractor.TryExtract(extendedUri, out string uri) ? uri : null;
                retransformUri = uri => $"{pre}{uri ?? "*"}{post}";
            }
        }

        public string Category => category;

        public bool Exists(string uri)
        {
            uri = transformUri(uri);
            if (uri == null) return false;
            return reader.Exists(uri);
        }

        public async Task<(bool, string[])> TryListUrisAsync(string uriPattern = null)
        {
            uriPattern = transformUri(uriPattern);
            if (uriPattern == null) return (false, default);
            if (!(await reader.TryListUrisAsync(uriPattern).ConfiguredAwait()).TryOut(out string[] uris)) return (false, default);
            uris = uris.Select(uri => retransformUri(uri)).Where(uri => uri != null).ToArray();
            return (true, uris);
        }

        public Task<(bool, T)> TryReadAsync<T>(string uri)
        {
            uri = transformUri(uri);
            if (uri == null) return Task.FromResult<(bool, T)>((false, default));
            return reader.TryReadAsync<T>(uri);
        }

        public Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer)
        {
            uri = transformUri(uri);
            if (uri == null) return Task.FromResult<bool>(false);
            return reader.TryReadAsync(uri, consumer);
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IMessageSink<ChangeNotification> notificationSink)
        {
            uriPattern = transformUri(uriPattern);
            if (uriPattern == null) return false;
            return reader.TrySubscribeForChangeNotifications(uriPattern, notificationSink);
        }
    }
}