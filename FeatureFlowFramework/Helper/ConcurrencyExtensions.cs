using System.Collections.Generic;

namespace FeatureFlowFramework.Helper
{
    public static class ConcurrencyExtensions
    {
        public static int LockedCount<T>(this ICollection<T> collection)
        {
            lock (collection)
            {
                return collection.Count;
            }
        }
    }
}