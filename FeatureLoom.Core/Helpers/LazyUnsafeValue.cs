using System;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides a zero-allocation, non-thread-safe lazy initializer for reference types with a parameterless constructor.
/// <para>
/// <b>Features:</b>
/// <list type="bullet">
/// <item>No thread safety: not safe for concurrent access from multiple threads.</item>
/// <item>Does not allocate memory for the wrapper itself (struct-based).</item>
/// <item>Only supports reference types with a public parameterless constructor.</item>
/// <item>Does not cache exceptions thrown during construction; each access retries construction if it failed.</item>
/// <item>Allows resetting the value via <see cref="RemoveObj"/>, enabling re-initialization on next access.</item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="T">Reference type with a public parameterless constructor.</typeparam>
public struct LazyUnsafeValue<T> where T : class, new()
{
    private T obj;

    /// <summary>
    /// Initializes the lazy value with an existing instance.
    /// </summary>
    /// <param name="obj">The initial value.</param>
    public LazyUnsafeValue(T obj)
    {
        this.obj = obj;
    }

    /// <summary>
    /// Gets the lazily initialized value, creating it if necessary using <c>new T()</c>.
    /// </summary>
    public T Obj
    {
        get => obj ?? Create();
        set => obj = value;
    }

    /// <summary>
    /// Gets the value if it exists, or <c>null</c> if it has not been created.
    /// </summary>
    public T ObjIfExists => obj;

    /// <summary>
    /// Indicates whether the value has been created.
    /// </summary>
    public bool Exists => obj != null;

    /// <summary>
    /// Removes the current value, allowing it to be re-initialized on next access.
    /// </summary>
    public void RemoveObj()
    {
        obj = default;
    }

    private T Create()
    {
        if (obj == null) obj = new T();
        return obj;
    }

    /// <summary>
    /// Implicitly converts the <see cref="LazyUnsafeValue{T}"/> to its underlying value.
    /// </summary>
    /// <param name="lazy">The <see cref="LazyUnsafeValue{T}"/> instance.</param>
    public static implicit operator T(LazyUnsafeValue<T> lazy) => lazy.Obj;

    /// <summary>
    /// Implicitly creates a <see cref="LazyUnsafeValue{T}"/> from an existing value.
    /// </summary>
    /// <param name="obj">The value to wrap.</param>
    public static implicit operator LazyUnsafeValue<T>(T obj) => new LazyUnsafeValue<T>(obj);
}