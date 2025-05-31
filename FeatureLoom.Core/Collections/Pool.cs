using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Collections
{
    /// <summary>
    /// Thread-safe or single-threaded object pool for reusing instances of <typeparamref name="T"/>.
    /// 
    /// The pool uses a stack to store reusable objects. When <see cref="Take"/> is called, an object is taken from the pool
    /// or created using the provided factory if the pool is empty. When <see cref="Return"/> is called, the object is optionally
    /// reset and returned to the pool if the pool has not reached its maximum size.
    /// The pool can be cleared with <see cref="Clear"/> and the current count of pooled objects is available via <see cref="Count"/>.
    /// </summary>
    public sealed class Pool<T> where T : class
    {
        Stack<T> stack = new Stack<T>();
        MicroLock myLock;
        Func<T> take;
        Action<T> ret;

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
        public T Take() => take();

        /// <summary>
        /// Returns an object to the pool. Optionally resets the object before returning.
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
        public void Clear()
        {
            using (myLock?.Lock())
            {
                stack.Clear();
            }
        }
    }
}
