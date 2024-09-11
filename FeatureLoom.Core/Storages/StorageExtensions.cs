using FeatureLoom.Extensions;
using FeatureLoom.MessageFlow;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{
    public static class StorageExtensions
    {
        public static bool TrySubscribeForChangeUpdate<T>(this IStorageReader reader, string uriPattern, IMessageSink<ChangeUpdate<T>> updateSink)
        {
            var converter = new MessageConverter<ChangeNotification, ChangeUpdate<T>>(note => new ChangeUpdate<T>(note, reader));
            if (reader.TrySubscribeForChangeNotifications(uriPattern, converter))
            {
                converter.ConnectTo(updateSink);
                return true;
            }
            return false;
        }

        public static async Task<bool> TryCopy(this IStorageReader reader, string sourceUri, IStorageWriter writer, string targetUri)
        {
            bool success = true;
            success &= await reader.TryReadAsync(sourceUri, async stream =>
            {
                success &= await writer.TryWriteAsync(targetUri, stream);                
            });
            return success;
        }

        public static Task<bool> TryCopy(this StorageService storage, string sourceCategory, string sourceUri, string targetCategory, string targetUri)
        {
            var reader = storage.GetReader(sourceCategory);
            var writer = storage.GetWriter(targetCategory);
            return reader.TryCopy(sourceUri, writer, targetUri);
        }
        
        public static async Task<bool> TryCopy(this IStorageReader reader, IStorageWriter writer, string uriFilterPattern = null)
        {
            if (!(await reader.TryListUrisAsync(uriFilterPattern)).TryOut(out var uris)) return false;
            bool success = true;
            foreach(var uri in uris)
            {
                success &= await TryCopy(reader, uri, writer, uri);
            }
            return success;
        }

        public static Task<bool> TryCopy(this StorageService storage, string sourceCategory, string targetCategory, string uriFilterPattern = null)
        {
            var reader = storage.GetReader(sourceCategory);
            var writer = storage.GetWriter(targetCategory);
            return reader.TryCopy(writer, uriFilterPattern);
        }

        public static async Task<bool> TryCopy(this IStorageReader reader, IStorageWriter writer, Func<string,string> uriConverter, string uriFilterPattern = null)
        {
            if (!(await reader.TryListUrisAsync(uriFilterPattern)).TryOut(out var uris)) return false;
            bool success = true;
            foreach (var uri in uris)
            {
                var targetUri = uriConverter(uri);
                if (targetUri == null) continue;
                success &= await TryCopy(reader, uri, writer, targetUri);
            }
            return success;
        }

        public static Task<bool> TryCopy(this StorageService storage, string sourceCategory, string targetCategory, Func<string, string> uriConverter, string uriFilterPattern = null)
        {
            var reader = storage.GetReader(sourceCategory);
            var writer = storage.GetWriter(targetCategory);
            return reader.TryCopy(writer, uriConverter, uriFilterPattern);
        }

        public async static Task<(bool, Dictionary<string,T>)> TryReadAllAsync<T>(this IStorageReader reader, string uriPattern = null)
        {
            if (!(await reader.TryListUrisAsync(uriPattern)).TryOut(out var uris)) return (false, null);
            Dictionary<string, T> dict = new();
            foreach(var uri in uris)
            {
                if (!(await reader.TryReadAsync<T>(uri)).TryOut(out var item)) return (false, null);
                dict[uri] = item;
            }
            return (true, dict);
        }                
    }
}