using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.Helper
{
    public static class MetaDataExtension
    {
        private static ConditionalWeakTable<object, Dictionary<string, object>> anchors = new ConditionalWeakTable<object, Dictionary<string, object>>();

        public static void SetMetaData<T, D>(this T obj, string key, D data) where T : class
        {            
            var metaData = anchors.GetOrCreateValue(obj);
            metaData[key] = data;
        }

        public static bool TryGetMetaData<T, D>(this T obj, string key, out D data) where T : class
        {
            var metaData = anchors.GetOrCreateValue(obj);
            if (metaData.TryGetValue(key, out object untyped) && untyped is D typed)
            {
                data = typed;
                return true;
            }
            data = default;
            return false;
        }
    }
}