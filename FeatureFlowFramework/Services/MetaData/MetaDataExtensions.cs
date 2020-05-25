using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.Services.MetaData
{
    public static class MetaDataExtensions
    {
        public static void SetMetaData<T, D>(this T obj, string key, D data) where T : class => MetaData<T>.SetMetaData(obj, key, data);

        public static bool TryGetMetaData<T, D>(this T obj, string key, out D data) where T : class => MetaData<T>.TryGetMetaData(obj, key, out data);        

        public static ObjectHandle GetHandle<T>(this T obj) where T:class => MetaData<T>.GetHandle(obj);

        public static FeatureLock GetLock<T>(this T obj) where T:class => MetaData<T>.GetLock(obj);        
    }
}