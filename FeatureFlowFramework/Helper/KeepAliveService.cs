using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.Helper
{
    public static class KeepAliveService
    {
        private static ConditionalWeakTable<object, List<object>> anchors = new ConditionalWeakTable<object, List<object>>();
        private static List<object> anchorless = new List<object>();

        public static T KeepAlive<T>(this T obj, object anchor = null) where T : class
        {
            if(anchor == null) anchorless.Add(obj);
            else
            {
                var anchorList = anchors.GetOrCreateValue(anchor);
                anchorList.Add(obj);
            }
            return obj;
        }
    }
}