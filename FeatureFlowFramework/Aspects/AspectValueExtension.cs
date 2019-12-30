using System;

namespace FeatureFlowFramework.Aspects
{
    public static class AspectValueExtension
    {
        public static T SetAspectValue<T>(this T obj, string key, object value, string collectionName = null) where T : class
        {
            var data = obj.GetAspectData();
            Predicate<IAspectValueContainer> predicate = null;
            if(collectionName != null) predicate = kv => kv.CollectionName == collectionName;
            if(data.TryGetAspectInterface(out IAspectValueContainer keyValue, predicate))
            {
                keyValue.Set(key, value);
            }
            else
            {
                var newKeyValue = new AspectValueAddOn(collectionName);
                newKeyValue.Set(key, value);
                data.AddAddOn(newKeyValue);
            }
            return obj;
        }

        public static bool TryGetAspectValue<T, V>(this T obj, string key, out V value, string collectionName = null) where T : class
        {
            var data = obj.GetAspectData();
            Predicate<IAspectValueContainer> predicate = null;
            if(collectionName != null) predicate = kv => kv.CollectionName == collectionName;
            if(data.TryGetAspectInterface(out IAspectValueContainer keyValue, predicate))
            {
                return keyValue.TryGet(key, out value);
            }
            value = default;
            return false;
        }
    }
}
