using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides a flexible implementation of <see cref="IEqualityComparer{T}"/> by allowing
/// custom equality and hash code logic to be supplied via delegates.
/// 
/// Note: This class serves as a replacement for <c>EqualityComparer&lt;T&gt;.Create</c>,
/// which is not available in .NET Standard.
/// </summary>
/// <typeparam name="T">The type of objects to compare.</typeparam>
public class DelegateEqualityComparer<T> : IEqualityComparer<T>
{
    private readonly Func<T, T, bool> equals;
    private readonly Func<T, int> getHashCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateEqualityComparer{T}"/> class.
    /// </summary>
    /// <param name="equals">
    /// A delegate that defines the method to determine equality between two objects.
    /// </param>
    /// <param name="getHashCode">
    /// A delegate that defines the method to compute the hash code for an object.
    /// If null, the type's default GetHashCode is used.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="equals"/> is null.
    /// </exception>
    public DelegateEqualityComparer(Func<T, T, bool> equals, Func<T, int> getHashCode = null)
    {
        this.equals = equals ?? throw new ArgumentNullException(nameof(equals));
        this.getHashCode = getHashCode ?? (obj => obj?.GetHashCode() ?? 0);
    }

    /// <summary>
    /// Determines whether the specified objects are equal using the provided equality delegate.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns>True if the objects are considered equal; otherwise, false.</returns>
    public bool Equals(T x, T y) => equals(x, y);

    /// <summary>
    /// Returns a hash code for the specified object using the provided hash code delegate.
    /// </summary>
    /// <param name="obj">The object for which to get a hash code.</param>
    /// <returns>A hash code for the specified object.</returns>
    public int GetHashCode(T obj) => getHashCode(obj);
}
