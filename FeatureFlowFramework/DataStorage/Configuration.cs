using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace FeatureFlowFramework.DataStorage
{
    public abstract class Configuration
    {
        public class Config : Configuration
        {
            public bool autoConfigWriteDefault = true;
            public Dictionary<string, bool> autoConfigWriteRules = new Dictionary<string, bool>();

            public Config()
            {
                try
                {
                    if(Reader.TryRead(this.Uri, out string json))
                    {
                        this.UpdateFromJson(json);
                    }
                    else if(autoConfigWriteDefault)
                    {
                        Writer.TryWrite(this.Uri, this);
                    }
                }
                catch
                {
                }
            }
        }

        private static Config config = new Config();

        public static bool AutoConfigWriteDefault
        {
            get => config.autoConfigWriteDefault;
            set => config.autoConfigWriteDefault = value;
        }

        public static void SetAutoConfigWriteRule(string configUri, bool set)
        {
            lock(config.autoConfigWriteRules)
            {
                config.autoConfigWriteRules[configUri] = set;
            }
        }

        public static void PerformAutoConfigWrite(Configuration configuration, bool ignore)
        {
            // This enforces the static constructor to be called, so its own config can be written
            if(ignore) return;

            lock(config.autoConfigWriteRules)
            {
                if(config.autoConfigWriteRules.TryGetValue(configuration.Uri, out bool write))
                {
                    if(write) configuration.TryWriteToStorage();
                }
                else if(config.autoConfigWriteDefault) configuration.TryWriteToStorage();
            }
        }

        [JsonIgnore]
        public virtual string Uri => this.GetType().FullName;

        [JsonIgnore]
        public virtual string ConfigCategory => "config";

        [JsonIgnore]
        protected LazySlim<LatestMessageReceiver<ChangeUpdate<string>>> subscriptionReceiver = new LazySlim<LatestMessageReceiver<ChangeUpdate<string>>>();

        [JsonIgnore]
        protected IStorageReader reader;

        [JsonIgnore]
        protected IStorageReader Reader
        {
            get
            {
                if(reader == null) reader = Storage.GetReader(ConfigCategory);
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
                if(writer == null) writer = Storage.GetWriter(ConfigCategory);
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

        public bool TryWriteToStorage()
        {
            return Writer.TryWrite(Uri, this);
        }

        public bool TryUpdateFromStorage(bool useSubscription)
        {
            bool success = false;
            string json = null;
            if(useSubscription)
            {
                if(!subscriptionReceiver.IsInstantiated)
                {
                    success = Reader.TryRead(this.Uri, out json);
                    PerformAutoConfigWrite(this, success);
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
                success = Reader.TryRead(this.Uri, out json);
                PerformAutoConfigWrite(this, success);
            }
            if(success)
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
