using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Collections;

/// <summary>
/// Wraps an <see cref="IEnumerable{T}"/> as a read-only <see cref="ICollection{T}"/>,
/// with lazy caching for efficient repeated access.
/// </summary>
public class EnumerableAsCollection<T> : ICollection<T>
{
    private IEnumerable<T> enumerable;
    private T[] cachedArray;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnumerableAsCollection{T}"/> class.
    /// </summary>
    /// <param name="enumerable">The enumerable to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerable"/> is null.</exception>
    public EnumerableAsCollection(IEnumerable<T> enumerable)
    {
        this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
    }

    /// <summary>
    /// Materializes the enumerable into an array if not already done, and returns the array.
    /// </summary>
    /// <returns>The cached array of items.</returns>
    private T[] GetArray()
    {
        if (cachedArray != null) return cachedArray;
        if (enumerable is T[] arr) cachedArray = arr;
        else cachedArray = enumerable.ToArray();
        enumerable = cachedArray; // Replace the enumerable with the array for future use
        return cachedArray;
    }

    /// <summary>
    /// Gets the number of elements in the collection.
    /// </summary>
    public int Count => GetArray().Length;

    /// <summary>
    /// Gets a value indicating whether the collection is read-only. Always true.
    /// </summary>
    public bool IsReadOnly => true;

    /// <summary>
    /// Not supported. This collection is read-only.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public void Add(T item)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Not supported. This collection is read-only.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public void Clear()
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Determines whether the collection contains a specific value.
    /// </summary>
    /// <param name="item">The object to locate in the collection.</param>
    /// <returns>True if item is found; otherwise, false.</returns>
    public bool Contains(T item)
    {
        return Array.IndexOf(GetArray(), item) >= 0;
    }

    /// <summary>
    /// Copies the elements of the collection to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    /// <exception cref="ArgumentNullException">If array is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If arrayIndex is negative.</exception>
    /// <exception cref="ArgumentException">If the destination array is not large enough.</exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        var src = GetArray();
        if (src.Length > array.Length - arrayIndex) throw new ArgumentException("Destination array is not large enough.");
        Array.Copy(src, 0, array, arrayIndex, src.Length);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)GetArray()).GetEnumerator();
    }

    /// <summary>
    /// Not supported. This collection is read-only.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public bool Remove(T item)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetArray().GetEnumerator();
    }
}
