using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{
    public class StorageWriterUriDecorator : IStorageWriter
    {
        string category;
        IStorageWriter writer;
        Func<string, string> transformUri;

        public StorageWriterUriDecorator(string category, IStorageWriter writer, Func<string, string> transformUri)
        {
            this.category = category;
            this.writer = writer;
            this.transformUri = transformUri;
        }

        public StorageWriterUriDecorator(string category, IStorageWriter writer, bool extendForStorage, string uriExtensionPattern)
        {
            this.category = category;
            this.writer = writer;

            string pre = uriExtensionPattern.Substring(0, "", "{uri}", out int restart);
            string post = uriExtensionPattern.Substring(restart, "{uri}", "", out _);
            var extractor = new PatternExtractor(uriExtensionPattern);
            if (extendForStorage)
            {
                transformUri = uri => $"{pre}{uri ?? "*"}{post}";
            }
            else
            {
                transformUri = extendedUri => extendedUri == null || extendedUri == "*" ? "*" : 
                                              extractor.TryExtract(extendedUri, out string uri) ? uri : null;                
            }
        }

        public string Category => category;

        public bool Exists(string uri)
        {
            uri = transformUri(uri);
            if (uri == null) return false;
            return writer.Exists(uri);
        }

        public Task<bool> TryAppendAsync<T>(string uri, T data)
        {
            uri = transformUri(uri);
            if (uri == null) return Task.FromResult(false);
            return writer.TryAppendAsync(uri, data);
        }

        public Task<bool> TryAppendAsync(string uri, Stream sourceStream)
        {
            uri = transformUri(uri);
            if (uri == null) return Task.FromResult(false);
            return writer.TryAppendAsync(uri, sourceStream);
        }

        public Task<bool> TryDeleteAsync(string uri)
        {
            uri = transformUri(uri);
            if (uri == null) return Task.FromResult(false);
            return writer.TryDeleteAsync(uri);
        }

        public Task<bool> TryWriteAsync<T>(string uri, T data)
        {
            uri = transformUri(uri);
            if (uri == null) return Task.FromResult(false);
            return writer.TryWriteAsync(uri, data);
        }

        public Task<bool> TryWriteAsync(string uri, Stream sourceStream)
        {
            uri = transformUri(uri);
            if (uri == null) return Task.FromResult(false);
            return writer.TryWriteAsync(uri, sourceStream);
        }
    }
}