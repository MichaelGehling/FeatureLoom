using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using System.Threading.Tasks;
using FeatureLoom.Extensions;
using System;
using System.Threading;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace FeatureLoom.Storages
{
    public abstract class Configuration
    {        
        public static string defaultCategory = "config";
        public static FeatureJsonDeserializer deserializer = new FeatureJsonDeserializer(new()
        {
            enableProposedTypes = false,
            dataAccess = FeatureJsonDeserializer.DataAccess.PublicAndPrivateFields,
            enableReferenceResolution = false,            
        });

        public static FeatureJsonSerializer serializer = new FeatureJsonSerializer(new()
        {
            indent = true,
            enumAsString = true,
            typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            referenceCheck = FeatureJsonSerializer.ReferenceCheck.NoRefCheck,
            dataSelection = FeatureJsonSerializer.DataSelection.PublicAndPrivateFields,            
        });

        [JsonIgnore]
        private string configUri;

        public static async Task DeleteAllConfigAsync()
        {
            var writer = Storage.GetWriter(defaultCategory);
            var reader = Storage.GetReader(defaultCategory);
            if ((await reader.TryListUrisAsync().ConfigureAwait(false)).TryOut(out string[] uris))
            {
                foreach (var uri in uris) await writer.TryDeleteAsync(uri).ConfigureAwait(false);
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
        protected LazyValue<LatestMessageReceiver<ChangeUpdate<string>>> subscriptionReceiver = new();

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

        public void StartSubscription()
        {
            if (subscriptionReceiver.Exists) return;

            if (!Reader.TrySubscribeForChangeUpdate<string>(Uri, subscriptionReceiver.Obj))
            {
                OptLog.ERROR()?.Build("Starting subscription for config object failed.", $"category={ConfigCategory} uri={Uri}");
            }
        }

        [JsonIgnore]
        public IAsyncWaitHandle SubscriptionWaitHandle => subscriptionReceiver.ObjIfExists?.WaitHandle ?? AsyncWaitHandle.NoWaitingHandle;

        [JsonIgnore]
        public bool HasSubscriptionUpdate => (!subscriptionReceiver.ObjIfExists?.IsEmpty) ?? false;

        public Task<bool> TryWriteToStorageAsync()
        {
            string json = serializer.Serialize(this);
            return Writer.TryWriteAsync(Uri, json);
        }

        public bool TryWriteToStorage()
        {
            string json = serializer.Serialize(this);
            return Writer.TryWriteAsync(Uri, json).WaitFor();
        }

        public async Task<bool> TryUpdateFromStorageAsync(bool useSubscription)
        {
            bool success = false;
            string json = null;
            if (useSubscription)
            {
                if (!subscriptionReceiver.Exists)
                {
                    success = (await Reader.TryReadAsync<string>(this.Uri).ConfigureAwait(false)).TryOut(out json);
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
                success = (await Reader.TryReadAsync<string>(this.Uri).ConfigureAwait(false)).TryOut(out json);
            }

            if (success)
            {                
                success = deserializer.TryPopulate(json, this);
            }

            if (success) OptLog.INFO()?.Build($"Configuration loaded for {Uri}!");            
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
                success = deserializer.TryPopulate(json, this);                
            }

            if (success) OptLog.INFO()?.Build($"Configuration loaded for {Uri}!");            
            return success;
        }
    }

    public static class ConfigurationExtensions
    {
        public static async Task TryUpdateFromStorageOrWriteAsync(this Configuration config, bool useSubscription)
        {
            if (!await config.TryUpdateFromStorageAsync(useSubscription).ConfigureAwait(false))
            {
                await config.TryWriteToStorageAsync().ConfigureAwait(false);
            }
        }

        public static async Task OnUpdateFromStorageApplyAndDo<T>(this T config, Action<T> action, CancellationToken cancellationToken = default) where T : Configuration
        {
            config.StartSubscription();
            await config.SubscriptionWaitHandle.OnEvent(cfg =>
            {
                if (cfg.TryUpdateFromStorage(true)) action(cfg);
            }, config, true, cancellationToken).ConfigureAwait(false);
        }

        public static async Task OnUpdateFromStorageApplyAndDo<T>(this T config, Func<T, Task> action, CancellationToken cancellationToken = default) where T : Configuration
        {
            config.StartSubscription();
            await config.SubscriptionWaitHandle.OnEvent(async cfg =>
            {
                if (cfg.TryUpdateFromStorage(true)) await action(cfg).ConfigureAwait(false);
            }, config, true, cancellationToken).ConfigureAwait(false);
        }

        public static void UpdateFromEnvironment<T>(this T config, string envVarPrefix = null) where T : Configuration
        {
            if (envVarPrefix == null) envVarPrefix = config.Uri + '.';

            var envs = Environment.GetEnvironmentVariables();

            Type configType = config.GetType();
            var fields = configType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                string varName = envVarPrefix + field.Name;
                if (!envs.Contains(varName)) continue;

                string jsonValue = envs[varName].ToString();
                if (field.FieldType == typeof(string)) jsonValue = '"' + jsonValue.TrimChar('"') + '"';
                if (Configuration.deserializer.TryDeserialize(jsonValue, field.FieldType, out object value))
                {
                    field.SetValue(config, value);
                }
            }
        }

        public static void UpdateFromArgs<T>(this T config, string[] args, string argumentPrefix = null) where T : Configuration
        {
            config.UpdateFromArgs(new ArgsHelper(args), argumentPrefix);
        }

        public static void UpdateFromArgs<T>(this T config, ArgsHelper argsHelper, string argumentPrefix = null) where T : Configuration
        {
            if (argumentPrefix == null) argumentPrefix = config.Uri + '.';            

            Type configType = config.GetType();
            var fields = configType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                string varName = argumentPrefix + field.Name;
                if (!argsHelper.HasKey(varName)) continue;

                var values = argsHelper.GetAllAfterKey(varName).ToList();
                if (values.Count == 0 && field.FieldType == typeof(bool)) values.Add("True");
                if (values.Count == 0) continue;

                string jsonValue;
                if (field.FieldType == typeof(string))
                {
                    if (values.Count > 1) OptLog.WARNING()?.Build($"For argument {varName} multiple values are defined. Only the first will be used. ");
                    jsonValue = '"' + values[0].TrimChar('"') + '"';
                }
                else if (field.FieldType.ImplementsGenericInterface(typeof(ICollection<>)))
                {
                    Type collectionType = field.FieldType.GetFirstTypeParamOfGenericInterface(typeof(ICollection<>));
                    if (collectionType == typeof(string)) values = values.Select(v => '"' + v.TrimChar('"') + '"').ToList();
                    jsonValue = "[" + values.AllItemsToString(",") + "]";
                }                
                else
                {
                    if (values.Count > 1) OptLog.WARNING()?.Build($"For argument {varName} multiple values are defined. Only the first will be used. ");
                    jsonValue = values[0];
                }

                if (Configuration.deserializer.TryDeserialize(jsonValue, field.FieldType, out object value))
                {
                    field.SetValue(config, value);
                }
                else
                {
                    jsonValue = '"' + jsonValue + '"';
                    if (Configuration.deserializer.TryDeserialize(jsonValue, field.FieldType, out value))
                    {
                        field.SetValue(config, value);
                    }
                    else
                    {
                        OptLog.WARNING()?.Build($"Failed to apply argument {varName}");
                    }
                }
            }
        }
    }
}