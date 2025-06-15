using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Synchronization
{
    /// <summary>
    /// Thread-safe wrapper for a reference type, providing read and write locking semantics.
    /// <para>
    /// Supports both eager and lazy initialization. Use the constructor with a factory delegate to enable lazy creation of the wrapped object.
    /// </para>
    /// <para>
    /// <b>Examples:</b>
    /// <code>
    /// // Eager initialization
    /// var lockedObj = new Locked&lt;MyClass&gt;(new MyClass());
    /// 
    /// // Lazy initialization
    /// var lockedLazy = new Locked&lt;MyClass&gt;(() =&gt; new MyClass());
    /// </code>
    /// </para>
    /// <b>Warning:</b> When using LINQ or deferred-execution queries on the protected object (e.g., collections), always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the lock scope. Do not enumerate or use the query outside the lock, as this is not thread-safe.
    /// </summary>
    /// <typeparam name="T">Reference type to be protected by the lock.</typeparam>
    public class MicroLocked<T> where T : class
    {
        MicroLock objLock = new MicroLock();
        LazyFactoryValue<T> lazy;

        /// <summary>
        /// Initializes a new instance of <see cref="MicroLocked{T}"/> with the specified object (eager initialization).
        /// </summary>
        /// <param name="obj">The object to be protected by the lock.</param>
        public MicroLocked(T obj)
        {
            this.lazy.ClearFactoryAfterConstruction = true;
            this.lazy.ThreadSafe = false; // Locked<T> is always thread-safe due to its own locking mechanism
            this.lazy.SetObj(obj);            
        }

        /// <summary>
        /// Initializes a new instance of <see cref="MicroLocked{T}"/> with a factory delegate for lazy initialization.
        /// The wrapped object will be created on first access, under the lock.
        /// </summary>
        /// <param name="lazyFactory">A delegate that creates the object when first needed.</param>
        /// <example>
        /// <code>
        /// var lockedLazy = new Locked&lt;MyClass&gt;(() =&gt; new MyClass());
        /// using (lockedLazy.UseWriteLocked(out var obj))
        /// {
        ///     obj.SomeProperty = 42;
        /// }
        /// </code>
        /// </example>
        public MicroLocked(Func<T> lazyFactory)
        {
            this.lazy = new LazyFactoryValue<T>(lazyFactory, threadSafe: false, clearFactoryAfterConstruction: true);
        }

        /// <summary>
        /// Acquires a write lock and returns the locked object and a lock handle.
        /// <para>
        /// <b>Usage:</b> Use the returned <see cref="MicroLock.LockHandle"/> in a <c>using</c> statement.
        /// The <paramref name="lockedObj"/> must <b>not</b> be used outside the <c>using</c> block.
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the using block. Do not enumerate or use the query outside the lock.
        /// </para>
        /// <example>
        /// <code>
        /// using (lockedObj.UseWriteLocked(out var obj))
        /// {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// }
        /// // Do NOT use obj or a deferred query here!
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="lockedObj">The object protected by the write lock. Only valid within the using block.</param>
        /// <returns>A <see cref="MicroLock.LockHandle"/> that must be disposed to release the lock.</returns>
        public MicroLock.LockHandle UseWriteLocked(out T lockedObj)
        {
            MicroLock.LockHandle handle = objLock.Lock();
            lockedObj = this.lazy;
            return handle;
        }

        /// <summary>
        /// Acquires a read-only lock and returns the locked object and a lock handle.
        /// <para>
        /// <b>Usage:</b> Use the returned <see cref="MicroLock.LockHandle"/> in a <c>using</c> statement.
        /// The <paramref name="readLockedObj"/> must <b>not</b> be used outside the <c>using</c> block.
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the using block. Do not enumerate or use the query outside the lock.
        /// </para>
        /// <example>
        /// <code>
        /// using (lockedObj.UseReadLocked(out var obj))
        /// {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// }
        /// // Do NOT use obj or a deferred query here!
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="readLockedObj">The object protected by the read lock. Only valid within the using block.</param>
        /// <returns>A <see cref="MicroLock.LockHandle"/> that must be disposed to release the lock.</returns>
        public MicroLock.LockHandle UseReadLocked(out T readLockedObj)
        {
            MicroLock.LockHandle handle = objLock.LockReadOnly();
            readLockedObj = this.lazy;
            return handle;
        }

        /// <summary>
        /// Sets the wrapped object under a write lock.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// lockedObj.Set(newObj);
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="obj">The new object to set.</param>
        public void Set(T obj)
        {
            using (objLock.Lock())
            {
                this.lazy.SetObj(obj);
            }
        }

        /// <summary>
        /// Gets the wrapped object without acquiring any lock.
        /// <para>
        /// <b>Warning:</b> This method is not thread-safe. Only use if you can guarantee external synchronization.
        /// </para>
        /// </summary>
        /// <returns>The wrapped object.</returns>
        public T GetUnlocked() => lazy;

        /// <summary>
        /// Attempts to acquire a write lock and returns the locked object and lock handle if successful.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// if (lockedObj.TryUseWriteLocked(out var handle, out var obj))
        /// using (handle)
        /// {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the using block. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <param name="lockHandle">The lock handle to be disposed if lock is acquired.</param>
        /// <param name="lockedObj">The object protected by the write lock. Only valid within the using block.</param>
        /// <returns>True if the lock was acquired, otherwise false.</returns>
        public bool TryUseLocked(out MicroLock.LockHandle lockHandle, out T lockedObj)
        {
            if (objLock.TryLock(out lockHandle))
            {
                lockedObj = this.lazy;
                return true;
            }
            else
            {
                lockedObj = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to acquire a read-only lock and returns the locked object and lock handle if successful.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// if (lockedObj.TryUseReadLocked(out var handle, out var obj))
        /// using (handle)
        /// {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the using block. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <param name="lockHandle">The lock handle to be disposed if lock is acquired.</param>
        /// <param name="readLockedObj">The object protected by the read lock. Only valid within the using block.</param>
        /// <returns>True if the lock was acquired, otherwise false.</returns>
        public bool TryUseReadLocked(out MicroLock.LockHandle lockHandle, out T readLockedObj)
        {
            if (objLock.TryLockReadOnly(out lockHandle))
            {
                readLockedObj = this.lazy;
                return true;
            }
            else
            {
                readLockedObj = null;
                return false;
            }
        }

        /// <summary>
        /// Executes an action under a write lock.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// lockedObj.UseLocked(obj => {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// });
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        public void UseLocked(Action<T> action)
        {
            using (objLock.Lock())
            {
                action(lazy);
            }
        }

        /// <summary>
        /// Executes a function under a write lock and returns its result.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// var snapshot = lockedObj.UseLocked(obj => obj.Where(x =&gt; x.IsActive).ToList()); // Safe
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public TResult UseLocked<TResult>(Func<T, TResult> func)
        {
            using (objLock.Lock())
            {
                return func(lazy);
            }
        }

        /// <summary>
        /// Executes an action under a read-only lock.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// lockedObj.UseReadLocked(obj => {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// });
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        public void UseReadLocked(Action<T> action)
        {
            using (objLock.LockReadOnly())
            {
                action(lazy);
            }
        }

        /// <summary>
        /// Executes a function under a read-only lock and returns its result.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// var snapshot = lockedObj.UseReadLocked(obj => obj.Where(x =&gt; x.IsActive).ToList()); // Safe
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public TResult UseReadLocked<TResult>(Func<T, TResult> func)
        {
            using (objLock.LockReadOnly())
            {
                return func(lazy);
            }
        }

        /// <summary>
        /// Attempts to execute an action under a write lock.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// lockedObj.TryUseLocked(obj => {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// });
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <returns>True if the lock was acquired and the action executed, otherwise false.</returns>
        public bool TryUseLocked(Action<T> action)
        {
            if (objLock.TryLock(out var lockHandle))
            using (lockHandle)
            {            
                action(lazy);
                return true;            
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a write lock and returns its result.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// if (lockedObj.TryUseLocked(obj => obj.Where(x =&gt; x.IsActive).ToList(), out var snapshot))
        /// {
        ///     // use snapshot
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="result">The result of the function if lock was acquired.</param>
        /// <returns>True if the lock was acquired and the function executed, otherwise false.</returns>
        public bool TryUseLocked<TResult>(Func<T, TResult> func, out TResult result)
        {
            if (objLock.TryLock(out var lockHandle))
            using (lockHandle)
            {
                result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Attempts to execute an action under a read-only lock.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// lockedObj.TryUseReadLocked(obj => {
        ///     var snapshot = obj.Where(x =&gt; x.IsActive).ToList(); // Safe
        /// });
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <returns>True if the lock was acquired and the action executed, otherwise false.</returns>
        public bool TryUseReadLocked(Action<T> action)
        {
            if (objLock.TryLockReadOnly(out var lockHandle))
            using (lockHandle)
            {
                action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a read-only lock and returns its result.
        /// <para>
        /// <b>Usage:</b>
        /// <code>
        /// if (lockedObj.TryUseReadLocked(obj => obj.Where(x =&gt; x.IsActive).ToList(), out var snapshot))
        /// {
        ///     // use snapshot
        /// }
        /// </code>
        /// </para>
        /// <para>
        /// <b>Warning:</b> When using LINQ or deferred-execution queries on the locked object, always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the delegate. Do not enumerate or use the query outside the lock.
        /// </para>
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="result">The result of the function if lock was acquired.</param>
        /// <returns>True if the lock was acquired and the function executed, otherwise false.</returns>
        public bool TryUseReadLocked<TResult>(Func<T, TResult> func, out TResult result)
        {
            if (objLock.TryLockReadOnly(out var lockHandle))
            using (lockHandle)
            {
                result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Indicates whether the object is currently locked (read or write).
        /// <para>
        /// <b>Note:</b> This property is for diagnostics only and should not be used for lock-free logic.
        /// </para>
        /// </summary>
        public bool IsLocked => objLock.IsLocked;
    }
}
