using System;
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
/// <remarks>
/// <para>
/// Reads acquire a snapshot of the stored reference; assignments publish it with release semantics.
/// Concurrent initializers may run multiple constructors, but return the same published instance unless
/// replacement or removal intervenes. Unpublished candidates are not disposed by this wrapper.
/// </para>
/// <para>
/// Removal and replacement do not cancel a constructor already running. An overlapping access may
/// return a detached instance, or publish a new instance after removal. Snapshots do not reserve the value
/// for subsequent operations, and the wrapped object's own operations are not synchronized.
/// </para>
/// <para>
/// This is a mutable struct: copies have independent storage. Use a shared, non-readonly field when
/// initialization must be retained; value copies and readonly receivers can initialize only a copy.
/// </para>
/// </remarks>
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
        get => Volatile.Read(ref obj) ?? Create();
        set => Volatile.Write(ref obj, value);
    }

    /// <summary>
    /// Gets a snapshot of the stored reference without creating it, or <c>null</c> if none is stored.
    /// </summary>
    public T ObjIfExists => Volatile.Read(ref obj);

    /// <summary>
    /// Gets a snapshot indicating whether a non-null reference is currently stored.
    /// </summary>
    public bool Exists => Volatile.Read(ref obj) != null;

    /// <summary>
    /// Atomically removes the current value without disposing it. An overlapping or later access may initialize it again.
    /// </summary>
    public void RemoveObj()
    {
        ExchangeObj(null);
    }

    /// <summary>
    /// Atomically replaces the stored reference and returns the previous reference without constructing or disposing either value.
    /// </summary>
    /// <param name="value">The replacement reference, or <c>null</c> to remove the stored reference.</param>
    /// <returns>The reference stored immediately before the exchange, or <c>null</c> if none was stored.</returns>
    public T ExchangeObj(T value)
    {
        return Interlocked.Exchange(ref obj, value);
    }

    private T Create()
    {
        var candidate = new T();
        // A separate field read could observe a concurrent removal instead of the publication result.
        return Interlocked.CompareExchange(ref obj, candidate, null) ?? candidate;
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