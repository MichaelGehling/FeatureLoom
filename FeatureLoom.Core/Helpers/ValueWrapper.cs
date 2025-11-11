using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Helpers;

/// <summary>
/// Non-generic abstraction for a wrapped value instance.
/// Implemented by <see cref="ValueWrapper{T}"/> to expose type information and validity state
/// without knowing the concrete generic parameter.
/// </summary>
public interface IValueWrapper
{
    /// <summary>
    /// Gets the type of the wrapped value.
    /// </summary>
    Type WrappedType { get; }

    /// <summary>
    /// Indicates whether the wrapper currently holds a valid value and is in an active (rented) state.
    /// </summary>
    bool IsValid { get; }
}

/// <summary>
/// Extremely lightweight, pooled wrapper for value types, optimized for peak-performance scenarios.
/// </summary>
/// <typeparam name="T">
/// The value type to wrap. Should be a struct or other value type, but not enforced
/// </typeparam>
/// <remarks>
/// - Ownership: Instances are single-owner and intended for single-threaded use while active.
/// - Lifecycle: Obtain via <see cref="Wrap(T)"/> and complete with a single call to <see cref="UnwrapAndDispose"/>.
/// - Pooling: Instances are rented from and returned to a shared pool to avoid allocations.
/// - Safety: <see cref="IsValid"/> guards against misuse (double return / use-after-return).
/// </remarks>
public sealed class ValueWrapper<T>
{
    /// <summary>
    /// Rents a wrapper from the pool and stores the supplied value.
    /// </summary>
    /// <param name="value">The value to be wrapped.</param>
    /// <returns>An active <see cref="ValueWrapper{T}"/> holding the specified value.</returns>
    /// <remarks>
    /// Pool initialization is done on first use for the closed generic type <typeparamref name="T"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueWrapper<T> Wrap(T value)
    {
        if (!SharedPool<ValueWrapper<T>>.IsInitialized)
        {
            // Single init per closed generic. Locking inside TryInit is already cheap.
            SharedPool<ValueWrapper<T>>.TryInit(
                onCreate: static () => new ValueWrapper<T>(),
                onReset: null,    // Intentionally no per-return callback; we overwrite on next Wrap.
                onDiscard: null,  // Discard silently.
                globalCapacity: 5000, localCapacity: 50, fetchOnEmpty: 45, keepOnFull: 5);
        }

        var wrapper = SharedPool<ValueWrapper<T>>.Take();
        wrapper.value = value;
        wrapper.active = true;        
        return wrapper;
    }

    private T value;

    /// <summary>
    /// Indicates whether this wrapper is currently active (rented) and holds a valid value.
    /// </summary>
    public bool IsValid => active;

    private volatile bool active;

    private ValueWrapper() { }

    /// <summary>
    /// Gets the type of the wrapped value (<see cref="T"/>).
    /// </summary>
    public Type WrappedType => typeof(T);

    /// <summary>
    /// Retrieves the contained value and returns this wrapper to the pool in a single, atomic operation.
    /// </summary>
    /// <returns>The wrapped value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the wrapper is not active (already returned to the pool).</exception>
    /// <remarks>
    /// After this call the instance is invalid and must not be used again.
    /// The internal value is cleared before the instance is returned to the pool to avoid retaining large structs.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T UnwrapAndDispose()
    {
        if (!active) throw new InvalidOperationException("ValueWrapper was already disposed or not active.");

        active = false;        
        var temp = value;
        value = default;
        SharedPool<ValueWrapper<T>>.Return(this);
        return temp;
    }
}
