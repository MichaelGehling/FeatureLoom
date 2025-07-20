using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides a simple, optionally thread-safe object pool for reusing instances of <typeparamref name="T"/>.
/// <para>
/// The pool stores reusable objects in a stack. When <see cref="Take"/> is called, an object is taken from the pool
/// or created using the provided factory if the pool is empty. When <see cref="Return"/> is called, the object is optionally
/// reset and returned to the pool if the pool has not reached its maximum size.
/// </para>
/// <para>
/// The pool can be cleared with <see cref="Clear"/> and the current count of pooled objects is available via <see cref="Count"/>.
/// </para>
/// <para>
/// <b>Thread Safety:</b> If threadSafe is true (default), all pool operations are protected by a <see cref="MicroLock"/>.
/// If false, no locking is performed and the pool is not safe for concurrent use.
/// </para>
/// </summary>
public sealed class Pool<T> where T : class
{
    // Stack to hold pooled objects.
    Stack<T> stack = new Stack<T>();
    // Lock for thread safety (if enabled).
    MicroLock myLock;
    // Delegate for taking an object from the pool.
    Func<T> take;
    // Delegate for returning an object to the pool.
    Action<T> ret;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pool{T}"/> class.
    /// </summary>
    /// <param name="create">A factory function to create new instances when the pool is empty.</param>
    /// <param name="reset">An optional action to reset objects before returning them to the pool.</param>
    /// <param name="maxSize">The maximum number of objects to keep in the pool. Defaults to 1000.</param>
    /// <param name="threadSafe">Whether the pool should be thread-safe. Defaults to true.</param>
    public Pool(Func<T> create, Action<T> reset = null, int maxSize = 1000, bool threadSafe = true)
    {
        if (threadSafe)
        {
            myLock = new MicroLock();
            take = () =>
            {
                using (myLock.Lock())
                {
                    if (stack.Count > 0) return stack.Pop();                        
                    else return create();
                }
            };

            ret = obj =>
            {
                using (myLock.Lock())
                {
                    if (stack.Count < maxSize)
                    {
                        reset?.Invoke(obj);
                        stack.Push(obj);
                    }
                }
            };
        }
        else if (reset != null)
        {
            take = () =>
            {
                if (stack.Count > 0) return stack.Pop();
                else return create();
            };

            ret = obj =>
            {
                if (stack.Count < maxSize)
                {
                    reset?.Invoke(obj);
                    stack.Push(obj);
                }
            };
        }
        else
        {
            take = () =>
            {
                if (stack.Count > 0) return stack.Pop();
                else return create();
            };

            ret = obj =>
            {
                if (stack.Count < maxSize)
                {                        
                    stack.Push(obj);
                }
            };
        }
    }

    /// <summary>
    /// Takes an object from the pool, or creates a new one if the pool is empty.
    /// </summary>
    /// <returns>
    /// An instance of <typeparamref name="T"/> from the pool, or a new instance if the pool is empty.
    /// </returns>
    public T Take() => take();

    /// <summary>
    /// Returns an object to the pool. Optionally resets the object before returning.
    /// If the pool has reached its maximum size, the object is not added.
    /// </summary>
    /// <param name="obj">The object to return to the pool.</param>
    public void Return(T obj) => ret(obj);

    /// <summary>
    /// Gets the current number of objects in the pool.
    /// </summary>
    public int Count => stack.Count;

    /// <summary>
    /// Removes all objects from the pool.
    /// </summary>
    /// <remarks>
    /// If the pool is thread-safe, this operation is protected by a lock.
    /// </remarks>
    public void Clear()
    {
        using (myLock?.Lock())
        {
            stack.Clear();
        }
    }
}
