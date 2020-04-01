using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataStorage
{
    public abstract class Configuration
    {
        public static string defaultCategory = "config";

        public static async Task DeleteAllConfigAsync()
        {
            var writer = Storage.GetWriter(defaultCategory);
            var reader = Storage.GetReader(defaultCategory);
            if ((await reader.TryListUrisAsync()).Out(out string[] uris))
            {
                foreach(var uri in uris) await writer.TryDeleteAsync(uri);
            }
        }

        [JsonIgnore]
        public virtual string Uri => this.GetType().FullName;        

        [JsonIgnore]
        public virtual string ConfigCategory => defaultCategory;

        [JsonIgnore]
        protected LazySlim<LatestMessageReceiver<ChangeUpdate<string>>> subscriptionReceiver = new LazySlim<LatestMessageReceiver<ChangeUpdate<string>>>();

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
                Log.ERROR("Starting subscription for config object failed.", $"category={ConfigCategory} uri={Uri}");
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
                if (!subscriptionReceiver.IsInstantiated)
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
                    Log.INFO(this, $"Configuration loaded for {Uri}!");
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