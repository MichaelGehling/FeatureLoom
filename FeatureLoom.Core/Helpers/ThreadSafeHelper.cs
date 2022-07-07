using System;
using System.Threading;

namespace FeatureLoom.Helpers
{
    public static class ThreadSafeHelper
    {
        /// <summary>
        /// Replaces an object instance by another instance that is provided based on the current object.
        /// Race conditions are mitigated by retrying it when the original object changed between provision
        /// of the new one and the assignment.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objRef"></param>
        /// <param name="provideNewObject"></param>
        /// <returns></returns>
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
