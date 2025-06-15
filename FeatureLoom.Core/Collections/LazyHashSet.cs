using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Collections;

/// <summary>
/// A lightweight, allocation-on-demand set wrapper that implements <see cref="ISet{T}"/>.
/// <para>
/// <b>LazyHashSet&lt;T&gt;</b> only allocates the underlying <see cref="HashSet{T}"/>
/// when a write operation (such as <see cref="Add"/>, <see cref="Remove"/>, or <see cref="UnionWith"/>) is performed.
/// Read operations (such as <see cref="Count"/>, <see cref="Contains"/>, or enumeration) are safe and return sensible
/// defaults (e.g., 0, false, or an empty enumerator) if the set has not been created.
/// </para>
/// <para>
/// This is useful for optional or rarely-used sets, reducing memory usage in scenarios where
/// many instances may never need to store any items. The struct supports implicit conversion to and
/// from <see cref="HashSet{T}"/> for easy interoperability.
/// </para>
/// <para>
/// <b>Note:</b> This struct is not thread-safe. Copying a <c>LazyHashSet&lt;T&gt;</c> copies the wrapper,
/// but both wrappers will reference the same underlying set if it has been created.
/// </para>
/// </summary>
public struct LazyHashSet<T> : ISet<T>
{
    private LazyUnsafeValue<HashSet<T>> set;

    /// <summary>
    /// Gets the underlying <see cref="HashSet{T}"/> instance, allocating it if necessary.
    /// </summary>
    public HashSet<T> GetSet()
    {
        return set.Obj;
    }

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// Returns 0 if the set has not been created.
    /// </summary>
    public int Count => set.Exists ? set.Obj.Count : 0;

    /// <summary>
    /// Gets a value indicating whether the set is read-only.
    /// Always returns false.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds an element to the set.
    /// Allocates the underlying set if it does not exist.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>True if the element is added; false if it is already present.</returns>
    public bool Add(T item)
    {
        return set.Obj.Add(item);
    }

    /// <summary>
    /// Adds an element to the set (explicit interface implementation).
    /// Allocates the underlying set if it does not exist.
    /// </summary>
    /// <param name="item">The element to add.</param>
    void ICollection<T>.Add(T item)
    {
        set.Obj.Add(item);
    }

    /// <summary>
    /// Modifies the current set to contain all elements that are present in itself, the specified collection, or both.
    /// Allocates the underlying set if it does not exist.
    /// </summary>
    /// <param name="other">The collection to union with.</param>
    public void UnionWith(IEnumerable<T> other)
    {
        set.Obj.UnionWith(other);
    }

    /// <summary>
    /// Modifies the current set to contain only elements that are also in a specified collection.
    /// Does nothing if the set has not been created.
    /// </summary>
    /// <param name="other">The collection to intersect with.</param>
    public void IntersectWith(IEnumerable<T> other)
    {
        if (!set.Exists) return;
        set.Obj.IntersectWith(other);
    }

    /// <summary>
    /// Removes all elements in the specified collection from the current set.
    /// Does nothing if the set has not been created.
    /// </summary>
    /// <param name="other">The collection of items to remove.</param>
    public void ExceptWith(IEnumerable<T> other)
    {
        if (!set.Exists) return;
        set.Obj.ExceptWith(other);
    }

    /// <summary>
    /// Modifies the current set to contain only elements that are present either in the set or in the specified collection, but not both.
    /// Does nothing if the set has not been created.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        if (!set.Exists) return;
        set.Obj.SymmetricExceptWith(other);
    }

    /// <summary>
    /// Determines whether the current set is a subset of a specified collection.
    /// Returns true if the set has not been created.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        if (!set.Exists) return true;
        return set.Obj.IsSubsetOf(other);
    }

    /// <summary>
    /// Determines whether the current set is a superset of a specified collection.
    /// Returns true if the set has not been created and the other collection is empty.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        if (!set.Exists) return !other.Any();
        return set.Obj.IsSupersetOf(other);
    }

    /// <summary>
    /// Determines whether the current set is a proper superset of a specified collection.
    /// Returns false if the set has not been created.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        if (!set.Exists) return false;
        return set.Obj.IsProperSupersetOf(other);
    }

    /// <summary>
    /// Determines whether the current set is a proper subset of a specified collection.
    /// Returns true if the set has not been created and the other collection is not empty.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        if (!set.Exists) return other.Any();
        return set.Obj.IsProperSubsetOf(other);
    }

    /// <summary>
    /// Determines whether the current set overlaps with the specified collection.
    /// Returns false if the set has not been created.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public bool Overlaps(IEnumerable<T> other)
    {
        if (!set.Exists) return false;
        return set.Obj.Overlaps(other);
    }

    /// <summary>
    /// Determines whether the current set and the specified collection contain the same elements.
    /// Returns true if the set has not been created and the other collection is empty.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public bool SetEquals(IEnumerable<T> other)
    {
        if (!set.Exists) return !other.Any();
        return set.Obj.SetEquals(other);
    }

    /// <summary>
    /// Removes all elements from the set.
    /// Does nothing if the set has not been created.
    /// </summary>
    public void Clear()
    {
        if (!set.Exists) return;
        set.Obj.Clear();
    }

    /// <summary>
    /// Determines whether the set contains a specific element.
    /// Returns false if the set has not been created.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    public bool Contains(T item)
    {
        if (!set.Exists) return false;
        return set.Obj.Contains(item);
    }

    /// <summary>
    /// Copies the elements of the set to an array, starting at a particular array index.
    /// Does nothing if the set has not been created.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (!set.Exists) return;
        set.Obj.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Removes the specified element from the set.
    /// Returns false if the set has not been created or the element is not found.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>True if the element was removed; otherwise, false.</returns>
    public bool Remove(T item)
    {
        if (!set.Exists) return false;
        return set.Obj.Remove(item);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// Returns an empty enumerator if the set has not been created.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        if (!set.Exists) return Enumerable.Empty<T>().GetEnumerator();
        return set.Obj.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set (non-generic).
    /// Returns an empty enumerator if the set has not been created.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator()
    {
        if (!set.Exists) return Enumerable.Empty<T>().GetEnumerator();
        return set.Obj.GetEnumerator();
    }

    /// <summary>
    /// Implicitly converts a <see cref="LazyHashSet{T}"/> to a <see cref="HashSet{T}"/>.
    /// </summary>
    public static implicit operator HashSet<T>(LazyHashSet<T> lazy) => lazy.GetSet();

    /// <summary>
    /// Implicitly converts a <see cref="HashSet{T}"/> to a <see cref="LazyHashSet{T}"/>.
    /// </summary>
    public static implicit operator LazyHashSet<T>(HashSet<T> obj)
    {
        var lazy = new LazyHashSet<T>();
        lazy.set.Obj = obj;
        return lazy;
    }
}