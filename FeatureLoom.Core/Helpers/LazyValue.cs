﻿using System;
using System.Threading;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides a zero-allocation, thread-safe lazy initializer for reference types with a parameterless constructor.
/// <para>
/// <b>Features:</b>
/// <list type="bullet">
/// <item>Does not allocate memory for the wrapper itself (struct-based).</item>
/// <item>Thread-safe initialization using <see cref="Interlocked.CompareExchange(ref object, object, object)"/>.</item>
/// <item>Only supports reference types with a public parameterless constructor.</item>
/// <item>Does not cache exceptions thrown during construction; each access retries construction if it failed.</item>
/// <item>Allows resetting the value via <see cref="RemoveObj"/>, enabling re-initialization on next access.</item>
/// </list>
/// </para>
/// </summary>
/// <typeparam name="T">Reference type with a public parameterless constructor.</typeparam>
public struct LazyValue<T> where T : class, new()
{
    private T obj;

    /// <summary>
    /// Initializes the lazy value with an existing instance.
    /// </summary>
    /// <param name="obj">The initial value.</param>
    public LazyValue(T obj)
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
        Interlocked.CompareExchange(ref obj, new T(), null);
        return obj;
    }

    /// <summary>
    /// Implicitly converts the <see cref="LazyValue{T}"/> to its underlying value.
    /// </summary>
    /// <param name="lazy">The <see cref="LazyValue{T}"/> instance.</param>
    public static implicit operator T(LazyValue<T> lazy) => lazy.Obj;

    /// <summary>
    /// Implicitly creates a <see cref="LazyValue{T}"/> from an existing value.
    /// </summary>
    /// <param name="obj">The value to wrap.</param>
    public static implicit operator LazyValue<T>(T obj) => new LazyValue<T>(obj);
}