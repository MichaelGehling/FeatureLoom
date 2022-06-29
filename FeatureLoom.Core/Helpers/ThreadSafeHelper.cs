using System;
using System.Threading;

namespace FeatureLoom.Helpers
{
    public static class ThreadSafeHelper
    {
        public static T ReplaceObject<T>(ref T objRef, Func<T, T> provideNewObject) where T : class
        {
            T oldObj;
            T newObj;
            do
            {
                oldObj = objRef;
                newObj = provideNewObject(oldObj);
            } while (oldObj != Interlocked.CompareExchange(ref objRef, newObj, oldObj));

            return newObj;
        }
    }
}
