using System;
using FeatureLoom.Helpers;

namespace FeatureLoom.Extensions
{
    public static class CloningExtensions
    {
        /// <summary>
        /// Creates a copy of the specified object based on the types ICloneable.Clone method and casts it to the specified type.
        /// </summary>
        /// <remarks>Use this method to create a copy of an object while preserving its type. The original
        /// object must implement a valid <see cref="ICloneable.Clone"/> method to ensure correct cloning
        /// behavior. Alternatively, you can use <see cref="DeepCloner.TryClone{T}"/> for generic deep cloning of arbitrary object graphs.</remarks>
        /// <typeparam name="T">The type of the object to clone. Must be a reference type that implements <see cref="ICloneable"/>.</typeparam>
        /// <param name="original">The object to clone. Cannot be null.</param>
        /// <returns>A new instance of <typeparamref name="T"/> that is a clone of the original object, or <see langword="null"/>
        /// if <paramref name="original"/> is <see langword="null"/>.</returns>
        public static T CloneAndCast<T>(this T original) where T : class, ICloneable
        {
            return (T)original?.Clone();
        }

        /// <summary>
        /// Creates a new array by cloning each element of the specified array individually.
        /// </summary>
        /// <remarks>Each element in the new array is cloned using the <c>ICloneable.Clone</c> method.
        /// Modifications to the elements in the returned array do not affect the original array.
        /// Alternatively, you can use <see cref="DeepCloner.TryClone{T}"/> for generic deep cloning of arbitrary object graphs.</remarks>
        /// <typeparam name="T">The type of the elements in the array. Must be a reference type that implements <see cref="ICloneable"/>.</typeparam>
        /// <param name="originalArray">The array whose elements are to be cloned. Can be null.</param>
        /// <returns>A new array containing clones of the elements from the original array, or null if <paramref
        /// name="originalArray"/> is null.</returns>
        public static T[] CloneArrayWithElements<T>(this T[] originalArray) where T : class, ICloneable
        {
            if (originalArray == null) return null;
            var newArray = (T[])originalArray.Clone();
            for (int i = 0; i < originalArray.Length; i++)
            {
                newArray[i] = originalArray[i].CloneAndCast();
            }
            return newArray;
        }

        /// <summary>
        /// Attempts to create a generic deep clone of <paramref name="obj"/>.
        /// </summary>
        /// <remarks>This method does not rely on the <see cref="ICloneable"/> interface and can clone arbitrary object graphs.</remarks>
        /// <typeparam name="T">The type of the object to clone.</typeparam>
        /// <param name="obj">The source object.</param>
        /// <param name="clone">The resulting deep clone if successful; otherwise default.</param>
        /// <returns><see langword="true"/> if cloning succeeded; otherwise <see langword="false"/>.</returns>
        public static bool TryCloneDeep<T>(this T obj, out T clone) => DeepCloner.TryClone(obj, out clone);
    }
}