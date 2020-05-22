using System;

namespace FeatureFlowFramework.Helpers.Extensions
{
    public static class CloningExtensions
    {
        public static T CloneAndCast<T>(this T original) where T : class, ICloneable
        {
            return (T)original?.Clone();
        }

        public static T[] CloneArrayWithElements<T>(this T[] originalArray) where T : class, ICloneable
        {
            if(originalArray == null) return null;
            var newArray = (T[])originalArray.Clone();
            for(int i = 0; i < originalArray.Length; i++)
            {
                newArray[i] = originalArray[i].CloneAndCast();
            }
            return newArray;
        }
    }
}