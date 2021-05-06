using FeatureLoom.DataFlows;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{
    public abstract class Configuration
    {
        public static string defaultCategory = "config";

        [JsonIgnore]
        private string configUri;

        public static async Task DeleteAllConfigAsync()
        {
            var writer = Storage.GetWriter(defaultCategory);
            var reader = Storage.GetReader(defaultCategory);
            if ((await reader.TryListUrisAsync()).Out(out string[] uris))
            {
                foreach (var uri in uris) await writer.TryDeleteAsync(uri);
            }
        }

        [JsonIgnore]
        public string Uri
        {
            get { return configUri ?? this.GetType().FullName; }
            set { configUri = value; }
        }

        [JsonIgnore]
        public bool IsUriDefault => configUri == default;

        [JsonIgnore]
        public virtual string ConfigCategory => defaultCategory;

        [JsonIgnore]
        protected LazyValue<LatestMessageReceiver<ChangeUpdate<string>>> subscriptionReceiver = new LazyValue<LatestMessageReceiver<ChangeUpdate<string>>>();

        [JsonIgnore]
        protected IStorageReader reader;

        [JsonIgnore]
        protected IStorageReader Reader
        {
            get
            {
                if (reader == null) reader = Storage.GetReader(ConfigCategory);
                return reader;
            }
        }

        [JsonIgnore]
        protected IStorageWriter writer;

        [JsonIgnore]
        protected IStorageWriter Writer
        {
            get
            {
                if (writer == null) writer = Storage.GetWriter(ConfigCategory);
                return writer;
            }
        }

        protected void StartSubscription()
        {
            if (!Reader.TrySubscribeForChangeUpdate<string>(Uri, subscriptionReceiver.Obj))
            {
                Log.ERROR(this.GetHandle(), "Starting subscription for config object failed.", $"category={ConfigCategory} uri={Uri}");
            }
        }

        [JsonIgnore]
        public IAsyncWaitHandle SubscriptionWaitHandle => subscriptionReceiver.ObjIfExists?.WaitHandle ?? AsyncWaitHandle.NoWaitingHandle;

        [JsonIgnore]
        public bool HasSubscriptionUpdate => (!subscriptionReceiver.ObjIfExists?.IsEmpty) ?? false;

        public async Task<bool> TryWriteToStorageAsync()
        {
            return await Writer.TryWriteAsync(Uri, this);
        }

        public async Task<bool> TryUpdateFromStorageAsync(bool useSubscription)
        {
            bool success = false;
            string json = null;
            if (useSubscription)
            {
                if (!subscriptionReceiver.Exists)
                {
                    success = (await Reader.TryReadAsync<string>(this.Uri)).Out(out json);
                    StartSubscription();
                }
                else
                {
                    if (this.subscriptionReceiver.Obj.TryReceive(out ChangeUpdate<string> changeUpdate))
                    {
                        success = changeUpdate.isValid;
                        json = changeUpdate.item;
                    }
                }
            }
            else
            {
                success = (await Reader.TryReadAsync<string>(this.Uri)).Out(out json);
            }
            if (success)
            {
                try
                {
                    this.UpdateFromJson(json);
                    Log.INFO(this.GetHandle(), $"Configuration loaded for {Uri}!");
                }
                catch
                {
                    success = false;
                }
            }
            return success;
        }

        public bool TryUpdateFromStorage(bool useSubscription)
        {
            bool success = false;
            string json = null;
            if (useSubscription)
            {
                if (!subscriptionReceiver.Exists)
                {
                    success = Reader.TryReadAsync<string>(this.Uri).WaitFor(out json);
                    StartSubscription();
                }
                else
                {
                    if (this.subscriptionReceiver.Obj.TryReceive(out ChangeUpdate<string> changeUpdate))
                    {
                        success = changeUpdate.isValid;
                        json = changeUpdate.item;
                    }
                }
            }
            else
            {
                success = Reader.TryReadAsync<string>(this.Uri).WaitFor(out json);
            }
            if (success)
            {
                try
                {
                    this.UpdateFromJson(json);
                    Log.INFO(this.GetHandle(), $"Configuration loaded for {Uri}!");
                }
                catch
                {
                    success = false;
                }
            }
            return success;
        }
    }
}