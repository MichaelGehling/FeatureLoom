using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Synchronization;
using System.Collections.Generic;

namespace FeatureFlowFramework.Aspects
{
    public class AspectValueAddOn : AspectAddOn, IAspectValueContainer
    {
        private readonly string collectionName;
        private Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
        FeatureLock keyValueLock = new FeatureLock();

        public AspectValueAddOn(string collectionName)
        {
            this.collectionName = collectionName;
        }

        public string CollectionName => collectionName;

        public void Set<T>(string key, T value)
        {
            using (keyValueLock.ForWriting()) keyValuePairs.Add(key, value);
        }

        public bool Contains(string key)
        {
            using (keyValueLock.ForReading())
            {
                return keyValuePairs.ContainsKey(key);
            }
        }

        public bool TryGet<T>(string key, out T value)
        {
            using (keyValueLock.ForReading())
            {
                if(keyValuePairs.TryGetValue(key, out object obj) && obj is T objT)
                {
                    value = objT;
                    return true;
                }
                else
                {
                    value = default;
                    return false;
                }
            }
        }
    }
}