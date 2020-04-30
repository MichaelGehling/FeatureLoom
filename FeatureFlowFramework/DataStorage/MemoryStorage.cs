using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;

namespace FeatureFlowFramework.DataStorage
{
    public class MemoryStorage : IStorageReaderWriter
    {
        public class Config : Configuration
        {
            public string configUri;
            public bool logFailedDeserialization = true;
            public int bufferSize = 1024;
            public override string Uri => configUri ?? base.Uri;
        }

        private readonly string category;
        private Config config;
        Dictionary<string, byte[]> dataSet = new Dictionary<string, byte[]>();
        FeatureLock dataSetLock = new FeatureLock();
        StorageSubscriptions subscriptions = new StorageSubscriptions();

        public MemoryStorage(string category, Config config = default)
        {
            this.category = category;
            if(config.configUri == null) config.configUri = "MemoryStorageConfig";
            config.configUri = config.Uri + "_" + this.category;
        }

        public string Category => category;

        public bool Exists(string uri)
        {
            using(dataSetLock.ForReading())
            {
                return dataSet.ContainsKey(uri);
            }
        }

        public async Task<bool> TryAppendAsync<T>(string uri, T data)
        {
            UpdateEvent updateEvent = UpdateEvent.Created; 
            bool success;
            using(await dataSetLock.ForWritingAsync())
            {
                if(TrySerialize(data, out byte[] newData))
                {
                    if(dataSet.TryGetValue(uri, out byte[] oldData))
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
                    success = true;
                }
                else success = false;
            }
            if(success) subscriptions.Notify(uri, this.category, updateEvent);
            return success;
        }

        public async Task<bool> TryAppendAsync(string uri, Stream sourceStream)
        {
            UpdateEvent updateEvent= UpdateEvent.Created;
            bool success;
            try
            {
                var newData = await sourceStream.ReadToByteArrayAsync(config.bufferSize);
                using(await dataSetLock.ForWritingAsync())
                {
                    if(!dataSet.TryGetValue(uri, out byte[] data))
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
                    success = true;
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this, $"Failed writing stream to MemoryStorage for uri {uri}!", e.ToString());
                success = false;
            }

            if(success) subscriptions.Notify(uri, this.category, updateEvent);
            return success;
        }        

        public async Task<bool> TryDeleteAsync(string uri)
        {
            bool removed;
            using(await dataSetLock.ForWritingAsync())
            {
                removed = dataSet.Remove(uri);                
            }
            if(removed) subscriptions.Notify(uri, this.category, UpdateEvent.Removed);
            return true;
        }

        public async Task<AsyncOutResult<bool, string[]>> TryListUrisAsync(string pattern = null)
        {
            using(await dataSetLock.ForReadingAsync())
            {
                if (!pattern.EmptyOrNull())
                {
                    var uris = dataSet.Keys.Where(uri => uri.MatchesWildcard(pattern)).ToArray();
                    return (true, uris);
                }
                else return (true, dataSet.Keys.ToArray());
            }
        }

        public async Task<AsyncOutResult<bool, T>> TryReadAsync<T>(string uri)
        {
            using(await dataSetLock.ForReadingAsync())
            {
                if(dataSet.TryGetValue(uri, out byte[] serializedData) &&
                   TryDeserialize(serializedData, out T data))
                {
                    return (true, data);
                }
                else return (false, default);
            }
        }

        public async Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer)
        {
            using(await dataSetLock.ForReadingAsync())
            {
                if(dataSet.TryGetValue(uri, out byte[] serializedData))
                {
                    await consumer(serializedData.ToStream());
                    return true;
                }
                else return false;
            }
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IDataFlowSink notificationSink)
        {
            subscriptions.Add(uriPattern, notificationSink);
            return true;
        }

        public async Task<bool> TryWriteAsync<T>(string uri, T data)
        {
            bool success;
            UpdateEvent updateEvent = UpdateEvent.Created;
            using(await dataSetLock.ForWritingAsync())
            {
                if(TrySerialize(data, out byte[] serializedData))
                {
                    if(dataSet.TryAdd(uri, serializedData))
                    {
                        updateEvent = UpdateEvent.Created;
                    }
                    else
                    {
                        dataSet[uri] = serializedData;
                        updateEvent = UpdateEvent.Updated;
                    }
                    success = true;
                }
                else success = false;
            }

            if(success) subscriptions.Notify(uri, this.category, updateEvent);
            return success;
        }

        public async Task<bool> TryWriteAsync(string uri, Stream sourceStream)
        {
            bool success;
            UpdateEvent updateEvent = UpdateEvent.Created;

            try
            {
                var data = await sourceStream.ReadToByteArrayAsync(config.bufferSize);
                using(await dataSetLock.ForWritingAsync())
                {
                    if(dataSet.TryAdd(uri, data))
                    {
                        updateEvent = UpdateEvent.Created;
                    }
                    else
                    {
                        dataSet[uri] = data;
                        updateEvent = UpdateEvent.Updated;
                    }
                    success = true;
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this, $"Failed writing stream to MemoryStorage for uri {uri}!", e.ToString());
                success = false;
            }

            if(success) subscriptions.Notify(uri, this.category, updateEvent);
            return success;
        }

        protected virtual bool TryDeserialize<T>(byte[] bytes, out T data)
        {
            data = default;

            if(bytes is T obj)
            {
                data = obj;
                return true;
            }
            else
            {
                try
                {
                    data = Encoding.UTF8.GetString(bytes).FromJson<T>();
                    return true;
                }
                catch(Exception e)
                {
                    if(config.logFailedDeserialization) Log.WARNING(this, "Failed on deserializing!", e.ToString());
                    data = default;
                    return false;
                }
            }
        }

        protected virtual bool TrySerialize<T>(T data, out byte[] bytes)
        {
            if(data is Byte[] byteData)
            {
                bytes = byteData.Clone() as byte[];
                return true;
            }
            else
            {
                try
                {
                    bytes = data.ToJson().ToByteArray(Encoding.UTF8);
                    return true;
                }
                catch(Exception e)
                {
                    Log.ERROR(this, "Failed serializing persiting object", e.ToString());
                    bytes = default;
                    return false;
                }
            }
        }
    }
}
