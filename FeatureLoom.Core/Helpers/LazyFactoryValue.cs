using System;
using System.Threading;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides a zero-allocation, optionally thread-safe lazy initializer for reference types using a custom factory delegate.
/// <para>
/// <b>Features:</b>
/// <list type="bullet">
/// <item>Supports custom factory delegate for value creation.</item>
/// <item>Thread-safe initialization using <see cref="Interlocked.CompareExchange(ref object, object, object)"/> when <c>threadSafe</c> is <c>true</c> (default).</item>
/// <item>Optionally disables thread safety for maximum performance when <c>threadSafe</c> is <c>false</c> (only safe for single-threaded use).</item>
/// <item>Optionally clears the factory after first use to minimize memory usage.</item>
/// <item>Allows resetting the value via <see cref="RemoveObj"/>; re-initialization depends on <c>clearFactoryAfterConstruction</c>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Behavior of <c>clearFactoryAfterConstruction</c>:</b>
/// <list type="bullet">
/// <item>If <c>true</c> (default): The factory is cleared after the first initialization. <see cref="RemoveObj"/> will make further initialization impossible, and accessing <see cref="Obj"/> will throw an <see cref="InvalidOperationException"/>.</item>
/// <item>If <c>false</c>: The factory is retained, allowing <see cref="RemoveObj"/> to reset and re-initialize the value multiple times.</item>
/// </list>
/// </para>
/// <para>
/// <b>Thread Safety:</b> If <c>threadSafe</c> is <c>true</c> (default), initialization is safe for concurrent access. If <c>false</c>, no synchronization is performed and the struct is only safe for single-threaded use.
/// </para>
/// </summary>
/// <typeparam name="T">Reference type to be lazily initialized.</typeparam>
public struct LazyFactoryValue<T> where T : class
{
    private T obj;
    private Func<T> factory;
    private bool threadSafe;
    private bool clearFactoryAfterConstruction;

    /// <summary>
    /// Initializes the lazy value with a custom factory.
    /// </summary>
    /// <param name="factory">The factory delegate used to create the value.</param>
    /// <param name="threadSafe">
    /// If <c>true</c> (default), initialization is thread-safe and safe for concurrent access.
    /// If <c>false</c>, no synchronization is performed and the struct is only safe for single-threaded use.
    /// </param>
    /// <param name="clearFactoryAfterConstruction">
    /// If <c>true</c> (default), the factory is cleared after first use and the value cannot be re-initialized after <see cref="RemoveObj"/>.
    /// If <c>false</c>, the factory is retained and the value can be re-initialized after <see cref="RemoveObj"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is null.</exception>
    public LazyFactoryValue(Func<T> factory, bool threadSafe = true, bool clearFactoryAfterConstruction = true)
    {
        this.obj = null;
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.threadSafe = threadSafe;
        this.clearFactoryAfterConstruction = clearFactoryAfterConstruction;
    }

    /// <summary>
    /// Gets the lazily initialized value, creating it if necessary using the provided factory.
    /// Throws <see cref="InvalidOperationException"/> if the factory was cleared and the value does not exist.
    /// </summary>
    public T Obj
    {
        get
        {
            if (obj != null) return obj;
            if (factory == null) throw new InvalidOperationException("Factory was already cleared.");
            var value = factory();
            if (threadSafe) Interlocked.CompareExchange(ref obj, value, null);
            else obj = value;
            if (clearFactoryAfterConstruction) factory = null;
            return obj;
        }
        set
        {
            obj = value;
            factory = null;
        }
    }

    /// <summary>
    /// Gets the value if it exists, or <c>null</c> if it has not been created.
    /// </summary>
    public T ObjIfExists => obj;

    /// <summary>
    /// Indicates whether the value has been created.
    /// </summary>
    public bool Exists => obj != null;

    public bool ThreadSafe { get => threadSafe; set => threadSafe = value; }
    public bool ClearFactoryAfterConstruction { get => clearFactoryAfterConstruction; set => clearFactoryAfterConstruction = value; }

    /// <summary>
    /// Removes the current value. If <c>clearFactoryAfterConstruction</c> is <c>true</c>, the factory is also cleared and the value cannot be re-initialized.
    /// </summary>
    public void RemoveObj()
    {
        obj = null;
        if (clearFactoryAfterConstruction) factory = null;
    }

    public void SetObj(T value)
    {
        if (threadSafe) Interlocked.CompareExchange(ref obj, value, null);
        else obj = value;
        if (clearFactoryAfterConstruction) factory = null;
    }

    /// <summary>
    /// Implicitly converts the <see cref="LazyFactoryValue{T}"/> to its underlying value.
    /// </summary>
    public static implicit operator T(LazyFactoryValue<T> lazy) => lazy.Obj;
}