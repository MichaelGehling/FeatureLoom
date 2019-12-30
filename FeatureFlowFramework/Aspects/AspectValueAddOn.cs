using FeatureFlowFramework.Helper;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Aspects
{

    public class AspectValueAddOn : AspectAddOn, IAspectValueContainer
    {
        readonly string collectionName;
        Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();

        public AspectValueAddOn(string collectionName)
        {
            this.collectionName = collectionName;            
        }

        public string CollectionName => collectionName;
        public void Set<T>(string key, T value)
        {
            lock(keyValuePairs) keyValuePairs.Add(key, value);
        }
        public bool Contains(string key)
        {
            lock(keyValuePairs)
            {
                return keyValuePairs.ContainsKey(key);
            }
        }
        public bool TryGet<T>(string key, out T value)
        {
            lock(keyValuePairs)
            {
                if (keyValuePairs.TryGetValue(key, out object obj) && obj is T objT)
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
