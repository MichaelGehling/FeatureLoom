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
        /// <typeparam name="T">The type of the object to be replaced. Must be a reference type.</typeparam>
        /// <param name="objRef">A reference to the variable holding the object to be replaced.</param>
        /// <param name="provideNewObject">A function that receives the current object and provides the new object that will replace it. This function may be called multiple times if a race condition occurs.</param>
        /// <returns>The new object that was set in the reference.</returns>
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
