/*using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using FeatureLoom.Serialization;
using FeatureLoom.Time;
using FeatureLoom.Collections;

namespace FeatureLoom.Storages
{
    public class EnvironmentVariableStorage : IStorageReaderWriter
    {
        public class Config : Configuration
        {
            public bool useCategoryPrefix = true;
            public bool allowSubscription = true;
            public TimeSpan subscriptionSamplingTime = 5.Seconds();
            public bool logFailedDeserialization = true;
            public bool splitObjectsIntoMembers = true;
        }

        private Config config;
        private readonly string category;
        EnvironmentVariableTarget target = EnvironmentVariableTarget.Process;

        public EnvironmentVariableStorage(string category, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process, Config config = default)
        {
            this.category = category;
            this.target = target;
            this.config = config ?? new Config();
        }

        public string Category => category;

        public bool Exists(string uri)
        {
            return Environment.GetEnvironmentVariables(target).Contains(uri);
        }

        public Task<bool> TryAppendAsync<T>(string uri, T data)
        {
            string variable = Environment.GetEnvironmentVariable(uri);
            if (variable == null) 
        }

        public Task<bool> TryAppendAsync(string uri, Stream sourceStream)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryDeleteAsync(string uri)
        {
            throw new NotImplementedException();
        }

        public Task<AsyncOut<bool, string[]>> TryListUrisAsync(string pattern = null)
        {
            throw new NotImplementedException();
        }

        public Task<(bool, T)> TryReadAsync<T>(string uri)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer)
        {
            throw new NotImplementedException();
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IMessageSink<ChangeNotification> notificationSink)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryWriteAsync<T>(string uri, T data)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryWriteAsync(string uri, Stream sourceStream)
        {
            throw new NotImplementedException();
        }

        protected virtual bool TryDeserialize<T>(string str, out T data)
        {
            data = default;

            if (str is T strObj)
            {
                data = strObj;
                return true;
            }
            else
            {
                try
                {
                    data = str.FromJson<T>();
                    return true;
                }
                catch (Exception e)
                {
                    if (config.logFailedDeserialization) Log.WARNING(this.GetHandle(), "Failed on deserializing!", e.ToString());
                    data = default;
                    return false;
                }
            }
        }

        protected virtual bool TrySerialize<T>(T data, out string str)
        {
            if (data is string strData)
            {
                str = strData;
                return true;
            }
            else
            {
                try
                {
                    str = data.ToJson();
                    return true;
                }
                catch (Exception e)
                {
                    Log.ERROR(this.GetHandle(), "Failed serializing persiting object", e.ToString());
                    str = default;
                    return false;
                }
            }
        }
    }
}
*/