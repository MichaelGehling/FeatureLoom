using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{
    public class MemoryStorage : IStorageReaderWriter
    {
        public class Config : Configuration
        {
            public bool logFailedDeserialization = true;
            public int bufferSize = 1024;
        }

        private readonly string category;
        private Config config;
        private Dictionary<string, byte[]> dataSet = new Dictionary<string, byte[]>();
        private FeatureLock dataSetLock = new FeatureLock();
        private StorageSubscriptions subscriptions = new StorageSubscriptions();

        public MemoryStorage(string category, Config config = default)
        {
            this.category = category;
            if (config == null) config = new Config();
            if (config.IsUriDefault) config.Uri = "MemoryStorageConfig" + "_" + this.category;
            this.config = config;
        }

        public string Category => category;

        public bool Exists(string uri)
        {
            using (dataSetLock.LockReadOnly())
            {
                return dataSet.ContainsKey(uri);
            }
        }

        public async Task<bool> TryAppendAsync<T>(string uri, T data)
        {
            UpdateEvent updateEvent = UpdateEvent.Created;
            bool success = TrySerialize(data, out byte[] newData);
            if (!success) return false;

            using (await dataSetLock.LockAsync().ConfigureAwait(false))
            {            
                if (dataSet.TryGetValue(uri, out byte[] oldData))
                {
                    var combined = oldData.Combine(newData);
                    dataSet[uri] = combined;
                    updateEvent = UpdateEvent.Updated;
                }
                else
                {
                    dataSet[uri] = newData;
                    updateEvent = UpdateEvent.Created;
                }
            }
            subscriptions.Notify(uri, this.category, updateEvent);
            return true;
        }

        public async Task<bool> TryAppendAsync(string uri, Stream sourceStream)
        {
            UpdateEvent updateEvent = UpdateEvent.Created;
            try
            {
                var newData = await sourceStream.ReadToByteArrayAsync(config.bufferSize).ConfigureAwait(false);
                using (await dataSetLock.LockAsync().ConfigureAwait(false))
                {
                    if (!dataSet.TryGetValue(uri, out byte[] data))
                    {
                        data = newData;
                        updateEvent = UpdateEvent.Created;
                    }
                    else
                    {
                        data = data.Combine(newData);
                        updateEvent = UpdateEvent.Updated;
                    }
                    dataSet[uri] = data;
                }
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed writing stream to MemoryStorage for uri {uri}!", e.ToString());
                return false;
            }

            subscriptions.Notify(uri, this.category, updateEvent);
            return true;
        }

        public async Task<bool> TryDeleteAsync(string uri)
        {
            bool removed;
            using (await dataSetLock.LockAsync().ConfigureAwait(false))
            {
                removed = dataSet.Remove(uri);
            }
            if (removed) subscriptions.Notify(uri, this.category, UpdateEvent.Removed);
            return true;
        }

        public async Task<(bool, string[])> TryListUrisAsync(string pattern = null)
        {
            using (await dataSetLock.LockReadOnlyAsync().ConfigureAwait(false))
            {
                if (!pattern.EmptyOrNull())
                {
                    var uris = dataSet.Keys.Where(uri => uri.MatchesWildcard(pattern)).ToArray();
                    return (true, uris);
                }
                else return (true, dataSet.Keys.ToArray());
            }
        }

        public async Task<(bool, T)> TryReadAsync<T>(string uri)
        {
            byte[] serializedData = null;
            using (await dataSetLock.LockReadOnlyAsync().ConfigureAwait(false))
            {
                if (!dataSet.TryGetValue(uri, out serializedData)) return (false, default);                
            }

            if (TryDeserialize(serializedData, out T data)) return (true, data);            
            else return (false, default);
        }

        public async Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer)
        {
            byte[] serializedData = null;            
            using (await dataSetLock.LockReadOnlyAsync().ConfigureAwait(false))
            {
                if (!dataSet.TryGetValue(uri, out serializedData)) return false;
            }            

            using (var stream = serializedData.ToStream())
            {
                await consumer(stream);
            }
            return true;            
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IMessageSink<ChangeNotification> notificationSink)
        {
            subscriptions.Add(uriPattern, notificationSink);
            return true;
        }

        public async Task<bool> TryWriteAsync<T>(string uri, T data)
        {
            UpdateEvent updateEvent = UpdateEvent.Created;
            if (!TrySerialize(data, out byte[] serializedData)) return false;

            using (await dataSetLock.LockAsync().ConfigureAwait(false))
            {                
                if (dataSet.TryAdd(uri, serializedData))
                {
                    updateEvent = UpdateEvent.Created;
                }
                else
                {
                    dataSet[uri] = serializedData;
                    updateEvent = UpdateEvent.Updated;
                }
            }

            subscriptions.Notify(uri, this.category, updateEvent);
            return true;
        }

        public async Task<bool> TryWriteAsync(string uri, Stream sourceStream)
        {
            UpdateEvent updateEvent = UpdateEvent.Created;
            try
            {
                var data = await sourceStream.ReadToByteArrayAsync(config.bufferSize).ConfigureAwait(false);
                using (await dataSetLock.LockAsync().ConfigureAwait(false))
                {
                    if (dataSet.TryAdd(uri, data))
                    {
                        updateEvent = UpdateEvent.Created;
                    }
                    else
                    {
                        dataSet[uri] = data;
                        updateEvent = UpdateEvent.Updated;
                    }
                }
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed writing stream to MemoryStorage for uri {uri}!", e.ToString());
                return false;
            }

            subscriptions.Notify(uri, this.category, updateEvent);
            return true;
        }

        protected virtual bool TryDeserialize<T>(byte[] bytes, out T data)
        {
            data = default;

            if (bytes is T byteArray)
            {
                data = byteArray;
                return true;
            }

            if (typeof(T) == typeof(string))
            {
                try
                {
                    string str = bytes.GetString(Encoding.UTF8);
                    if (str is T strT) data = strT;
                    return true;
                }
                catch (Exception e)
                {
                    if (config.logFailedDeserialization) OptLog.WARNING()?.Build("Failed on deserializing! bytes is no proper UTF8 string!", e.ToString());
                    data = default;
                    return false;
                }
            }
                        
            if (JsonHelper.DefaultDeserializer.TryDeserialize(bytes, out data)) return true;
            else
            {
                if (config.logFailedDeserialization) OptLog.WARNING()?.Build("Failed on deserializing!");
                data = default;
                return false;
            }            
        }

        protected virtual bool TrySerialize<T>(T data, out byte[] bytes)
        {
            if (data is Byte[] byteData)
            {
                bytes = byteData.Clone() as byte[];
                return true;
            }

            if (data is string str)
            {
                bytes = str.ToByteArray(Encoding.UTF8);
                return true;
            }

            try
            {
                MemoryStream stream = new MemoryStream();
                JsonHelper.DefaultSerializer.Serialize(stream, data);
                bytes = stream.ToArray();
                return true;
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build("Failed serializing persisting object", e.ToString());
                bytes = default;
                return false;
            }
        }
    }
}