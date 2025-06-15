using FeatureLoom.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization
{
    /// <summary>
    /// Thread-safe wrapper for a reference type, providing advanced locking semantics using <see cref="FeatureLock"/>.
    /// <para>
    /// Supports both eager and lazy initialization. Use the constructor with a factory delegate to enable lazy creation of the wrapped object.
    /// </para>
    /// <para>
    /// <b>Features:</b>
    /// <list type="bullet">
    /// <item>Read/write locks</item>
    /// <item>Upgradeable locks (upgrade/downgrade via handle)</item>
    /// <item>Async/await support</item>
    /// <item>Timeouts and try-based lock acquisition</item>
    /// </list>
    /// <b>Examples:</b>
    /// <code>
    /// // Try to acquire a write lock with timeout
    /// if (lockedObj.TryUseWriteLocked(TimeSpan.FromMilliseconds(100), out var handle, out var obj))
    /// using (handle)
    /// {
    ///     obj.SomeProperty = 42;
    /// }
    /// </code>
    /// </para>
    /// <b>Warning:</b> When using LINQ or deferred-execution queries on the protected object (e.g., collections), always materialize the result (e.g., with <c>.ToList()</c> or <c>.ToArray()</c>) inside the lock scope. Do not enumerate or use the query outside the lock, as this is not thread-safe.
    /// <para>
    /// <b>Note:</b> Upgrading and downgrading between read and write lock is only possible when using the handle-based API (see <c>UseReadLocked</c> and <c>UseWriteLocked</c>).
    /// </para>
    /// </summary>
    /// <typeparam name="T">Reference type to be protected by the lock.</typeparam>
    public class FeatureLocked<T> where T : class
    {
        FeatureLock objLock = new FeatureLock();
        LazyFactoryValue<T> lazy;

        /// <summary>
        /// Initializes a new instance of <see cref="FeatureLocked{T}"/> with the specified object (eager initialization).
        /// </summary>
        /// <param name="obj">The object to be protected by the lock.</param>
        public FeatureLocked(T obj)
        {
            this.lazy.ClearFactoryAfterConstruction = true;
            this.lazy.ThreadSafe = false;
            this.lazy.SetObj(obj);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FeatureLocked{T}"/> with a factory delegate for lazy initialization.
        /// The wrapped object will be created on first access, under the lock.
        /// </summary>
        /// <param name="lazyFactory">A delegate that creates the object when first needed.</param>
        public FeatureLocked(Func<T> lazyFactory)
        {
            this.lazy = new LazyFactoryValue<T>(lazyFactory, threadSafe: false, clearFactoryAfterConstruction: true);
        }

        // --- Handle-based Lock Methods ---

        /// <summary>
        /// Acquires a write lock and returns the locked object and a lock handle.
        /// </summary>
        /// <param name="lockedObj">The object protected by the write lock.</param>
        /// <returns>A lock handle that must be disposed to release the lock.</returns>
        public FeatureLock.LockHandle UseWriteLocked(out T lockedObj)
        {
            var handle = objLock.Lock();
            lockedObj = this.lazy;
            return handle;
        }

        /// <summary>
        /// Acquires a read-only lock and returns the locked object and a lock handle.
        /// </summary>
        /// <param name="readLockedObj">The object protected by the read lock.</param>
        /// <returns>A lock handle that must be disposed to release the lock.</returns>
        public FeatureLock.LockHandle UseReadLocked(out T readLockedObj)
        {
            var handle = objLock.LockReadOnly();
            readLockedObj = this.lazy;
            return handle;
        }

        /// <summary>
        /// Asynchronously acquires a write lock and returns the locked object and a lock handle.
        /// </summary>
        /// <returns>A tuple containing the lock handle and the locked object.</returns>
        public async Task<(FeatureLock.LockHandle Handle, T LockedObj)> UseWriteLockedAsync()
        {
            var handle = await objLock.LockAsync().ConfigureAwait(false);
            return (handle, this.lazy);
        }

        /// <summary>
        /// Asynchronously acquires a read-only lock and returns the locked object and a lock handle.
        /// </summary>
        /// <returns>A tuple containing the lock handle and the locked object.</returns>
        public async Task<(FeatureLock.LockHandle Handle, T LockedObj)> UseReadLockedAsync()
        {
            var handle = await objLock.LockReadOnlyAsync().ConfigureAwait(false);
            return (handle, this.lazy);
        }

        // --- Try-based Lock Methods (immediate) ---

        /// <summary>
        /// Attempts to acquire a write lock immediately.
        /// </summary>
        /// <param name="handle">The lock handle if acquired.</param>
        /// <param name="lockedObj">The object if lock was acquired, otherwise null.</param>
        /// <returns>True if lock was acquired, otherwise false.</returns>
        public bool TryUseWriteLocked(out FeatureLock.LockHandle handle, out T lockedObj)
        {
            if (objLock.TryLock(out handle))
            {
                lockedObj = this.lazy;
                return true;
            }
            lockedObj = null;
            return false;
        }

        /// <summary>
        /// Attempts to acquire a read-only lock immediately.
        /// </summary>
        /// <param name="handle">The lock handle if acquired.</param>
        /// <param name="readLockedObj">The object if lock was acquired, otherwise null.</param>
        /// <returns>True if lock was acquired, otherwise false.</returns>
        public bool TryUseReadLocked(out FeatureLock.LockHandle handle, out T readLockedObj)
        {
            if (objLock.TryLockReadOnly(out handle))
            {
                readLockedObj = this.lazy;
                return true;
            }
            readLockedObj = null;
            return false;
        }

        // --- Try-based Lock Methods (with timeout) ---

        /// <summary>
        /// Attempts to acquire a write lock within the specified timeout period.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="handle">The lock handle if acquired.</param>
        /// <param name="lockedObj">The object if lock was acquired, otherwise null.</param>
        /// <returns>True if lock was acquired, otherwise false.</returns>
        public bool TryUseWriteLocked(TimeSpan timeout, out FeatureLock.LockHandle handle, out T lockedObj)
        {
            if (objLock.TryLock(timeout, out handle))
            {
                lockedObj = this.lazy;
                return true;
            }
            lockedObj = null;
            return false;
        }

        /// <summary>
        /// Attempts to acquire a read-only lock within the specified timeout period.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="handle">The lock handle if acquired.</param>
        /// <param name="readLockedObj">The object if lock was acquired, otherwise null.</param>
        /// <returns>True if lock was acquired, otherwise false.</returns>
        public bool TryUseReadLocked(TimeSpan timeout, out FeatureLock.LockHandle handle, out T readLockedObj)
        {
            if (objLock.TryLockReadOnly(timeout, out handle))
            {
                readLockedObj = this.lazy;
                return true;
            }
            readLockedObj = null;
            return false;
        }

        // --- Async Try-based Lock Methods (with timeout) ---

        /// <summary>
        /// Asynchronously attempts to acquire a write lock within the specified timeout period.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>A tuple indicating success, the lock handle, and the locked object.</returns>
        public async Task<(bool Success, FeatureLock.LockHandle Handle, T LockedObj)> TryUseWriteLockedAsync(TimeSpan timeout)
        {
            var attempt = await objLock.TryLockAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                return (true, handle, this.lazy);
            }
            return (false, default, null);
        }

        /// <summary>
        /// Asynchronously attempts to acquire a read-only lock within the specified timeout period.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>A tuple indicating success, the lock handle, and the locked object.</returns>
        public async Task<(bool Success, FeatureLock.LockHandle Handle, T LockedObj)> TryUseReadLockedAsync(TimeSpan timeout)
        {
            var attempt = await objLock.TryLockReadOnlyAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                return (true, handle, this.lazy);
            }
            return (false, default, null);
        }

        // --- Set and Get ---

        /// <summary>
        /// Sets the wrapped object under a write lock.
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
        /// </summary>
        /// <returns>The wrapped object.</returns>
        public T GetUnlocked() => lazy;

        // --- Delegate-based Lock Methods (no upgradability) ---

        /// <summary>
        /// Executes an action under a write lock.
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
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
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
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
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
        /// Executes an action under a reentrant read-only lock.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        public void UseReentrantReadLocked(Action<T> action)
        {
            using (objLock.LockReentrantReadOnly())
            {
                action(lazy);
            }
        }

        /// <summary>
        /// Executes a function under a reentrant read-only lock and returns its result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public TResult UseReentrantReadLocked<TResult>(Func<T, TResult> func)
        {
            using (objLock.LockReentrantReadOnly())
            {
                return func(lazy);
            }
        }

        /// <summary>
        /// Executes an action under a reentrant write lock.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        public void UseReentrantWriteLocked(Action<T> action)
        {
            using (objLock.LockReentrant())
            {
                action(lazy);
            }
        }

        /// <summary>
        /// Executes a function under a reentrant write lock and returns its result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public TResult UseReentrantWriteLocked<TResult>(Func<T, TResult> func)
        {
            using (objLock.LockReentrant())
            {
                return func(lazy);
            }
        }

        // --- Try-based Delegate Lock Methods (immediate) ---

        /// <summary>
        /// Attempts to execute an action under a write lock without waiting.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseLocked(Action<T> action)
        {
            if (objLock.TryLock(out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a write lock without waiting and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseLocked<TResult>(Func<T, TResult> func, out TResult result)
        {
            if (objLock.TryLock(out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Attempts to execute an action under a read-only lock without waiting.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseReadLocked(Action<T> action)
        {
            if (objLock.TryLockReadOnly(out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a read-only lock without waiting and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseReadLocked<TResult>(Func<T, TResult> func, out TResult result)
        {
            if (objLock.TryLockReadOnly(out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Attempts to execute an action under a reentrant read-only lock without waiting.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseReentrantReadLocked(Action<T> action)
        {
            if (objLock.TryLockReentrantReadOnly(out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a reentrant read-only lock without waiting and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseReentrantReadLocked<TResult>(Func<T, TResult> func, out TResult result)
        {
            if (objLock.TryLockReentrantReadOnly(out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Attempts to execute an action under a reentrant write lock without waiting.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseReentrantWriteLocked(Action<T> action)
        {
            if (objLock.TryLockReentrant(out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a reentrant write lock without waiting and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseReentrantWriteLocked<TResult>(Func<T, TResult> func, out TResult result)
        {
            if (objLock.TryLockReentrant(out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        // --- Try-based Delegate Lock Methods (with timeout) ---

        /// <summary>
        /// Attempts to execute an action under a write lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseLocked(Action<T> action, TimeSpan timeout)
        {
            if (objLock.TryLock(timeout, out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a write lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseLocked<TResult>(Func<T, TResult> func, TimeSpan timeout, out TResult result)
        {
            if (objLock.TryLock(timeout, out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Attempts to execute an action under a read-only lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseReadLocked(Action<T> action, TimeSpan timeout)
        {
            if (objLock.TryLockReadOnly(timeout, out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a read-only lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseReadLocked<TResult>(Func<T, TResult> func, TimeSpan timeout, out TResult result)
        {
            if (objLock.TryLockReadOnly(timeout, out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Attempts to execute an action under a reentrant read-only lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseReentrantReadLocked(Action<T> action, TimeSpan timeout)
        {
            if (objLock.TryLockReentrantReadOnly(timeout, out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a reentrant read-only lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseReentrantReadLocked<TResult>(Func<T, TResult> func, TimeSpan timeout, out TResult result)
        {
            if (objLock.TryLockReentrantReadOnly(timeout, out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Attempts to execute an action under a reentrant write lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public bool TryUseReentrantWriteLocked(Action<T> action, TimeSpan timeout)
        {
            if (objLock.TryLockReentrant(timeout, out var handle))
            {
                using (handle) action(lazy);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute a function under a reentrant write lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="result">The result of the function if the lock was acquired, or the default value if not.</param>
        /// <returns>True if the lock was acquired and the function was executed; otherwise, false.</returns>
        public bool TryUseReentrantWriteLocked<TResult>(Func<T, TResult> func, TimeSpan timeout, out TResult result)
        {
            if (objLock.TryLockReentrant(timeout, out var handle))
            {
                using (handle) result = func(lazy);
                return true;
            }
            result = default;
            return false;
        }

        // --- Async Delegate-based Try Lock Methods (with timeout) ---

        /// <summary>
        /// Asynchronously attempts to execute an action under a write lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public async Task<bool> TryUseLockedAsync(Func<T, Task> action, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) await action(lazy).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Asynchronously attempts to execute a function under a write lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>A tuple indicating success and the result of the function.</returns>
        public async Task<(bool Success, TResult Result)> TryUseLockedAsync<TResult>(Func<T, Task<TResult>> func, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) return (true, await func(lazy).ConfigureAwait(false));
            }
            return (false, default);
        }

        /// <summary>
        /// Asynchronously attempts to execute an action under a read-only lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public async Task<bool> TryUseReadLockedAsync(Func<T, Task> action, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockReadOnlyAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) await action(lazy).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Asynchronously attempts to execute a function under a read-only lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>A tuple indicating success and the result of the function.</returns>
        public async Task<(bool Success, TResult Result)> TryUseReadLockedAsync<TResult>(Func<T, Task<TResult>> func, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockReadOnlyAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) return (true, await func(lazy).ConfigureAwait(false));
            }
            return (false, default);
        }

        /// <summary>
        /// Asynchronously attempts to execute an action under a reentrant read-only lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public async Task<bool> TryUseReentrantReadLockedAsync(Func<T, Task> action, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockReentrantReadOnlyAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) await action(lazy).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Asynchronously attempts to execute a function under a reentrant read-only lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>A tuple indicating success and the result of the function.</returns>
        public async Task<(bool Success, TResult Result)> TryUseReentrantReadLockedAsync<TResult>(Func<T, Task<TResult>> func, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockReentrantReadOnlyAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) return (true, await func(lazy).ConfigureAwait(false));
            }
            return (false, default);
        }

        /// <summary>
        /// Asynchronously attempts to execute an action under a reentrant write lock within the specified timeout period.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>True if the lock was acquired and the action was executed; otherwise, false.</returns>
        public async Task<bool> TryUseReentrantWriteLockedAsync(Func<T, Task> action, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockReentrantAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) await action(lazy).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Asynchronously attempts to execute a function under a reentrant write lock within the specified timeout period and returns its result if successful.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <returns>A tuple indicating success and the result of the function.</returns>
        public async Task<(bool Success, TResult Result)> TryUseReentrantWriteLockedAsync<TResult>(Func<T, Task<TResult>> func, TimeSpan timeout)
        {
            var attempt = await objLock.TryLockReentrantAsync(timeout).ConfigureAwait(false);
            if (attempt.Succeeded(out var handle))
            {
                using (handle) return (true, await func(lazy).ConfigureAwait(false));
            }
            return (false, default);
        }

        // --- Async Delegate-based Lock Methods (no upgradability) ---

        /// <summary>
        /// Asynchronously executes an action under a write lock.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        public async Task UseLockedAsync(Func<T, Task> action)
        {
            using (await objLock.LockAsync().ConfigureAwait(false))
            {
                await action(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes a function under a write lock and returns its result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public async Task<TResult> UseLockedAsync<TResult>(Func<T, Task<TResult>> func)
        {
            using (await objLock.LockAsync().ConfigureAwait(false))
            {
                return await func(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes an action under a read-only lock.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        public async Task UseReadLockedAsync(Func<T, Task> action)
        {
            using (await objLock.LockReadOnlyAsync().ConfigureAwait(false))
            {
                await action(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes a function under a read-only lock and returns its result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public async Task<TResult> UseReadLockedAsync<TResult>(Func<T, Task<TResult>> func)
        {
            using (await objLock.LockReadOnlyAsync().ConfigureAwait(false))
            {
                return await func(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes an action under a write lock.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        public async Task UseWriteLockedAsync(Func<T, Task> action)
        {
            using (await objLock.LockAsync().ConfigureAwait(false))
            {
                await action(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes a function under a write lock and returns its result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public async Task<TResult> UseWriteLockedAsync<TResult>(Func<T, Task<TResult>> func)
        {
            using (await objLock.LockAsync().ConfigureAwait(false))
            {
                return await func(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes an action under a reentrant read-only lock.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        public async Task UseReentrantReadLockedAsync(Func<T, Task> action)
        {
            using (await objLock.LockReentrantReadOnlyAsync().ConfigureAwait(false))
            {
                await action(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes a function under a reentrant read-only lock and returns its result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public async Task<TResult> UseReentrantReadLockedAsync<TResult>(Func<T, Task<TResult>> func)
        {
            using (await objLock.LockReentrantReadOnlyAsync().ConfigureAwait(false))
            {
                return await func(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes an action under a reentrant write lock.
        /// </summary>
        /// <param name="action">The async action to execute with the locked object.</param>
        public async Task UseReentrantWriteLockedAsync(Func<T, Task> action)
        {
            using (await objLock.LockReentrantAsync().ConfigureAwait(false))
            {
                await action(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously executes a function under a reentrant write lock and returns its result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="func">The async function to execute with the locked object.</param>
        /// <returns>The result of the function.</returns>
        public async Task<TResult> UseReentrantWriteLockedAsync<TResult>(Func<T, Task<TResult>> func)
        {
            using (await objLock.LockReentrantAsync().ConfigureAwait(false))
            {
                return await func(lazy).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Indicates whether the object is currently locked (read or write).
        /// </summary>
        public bool IsLocked => objLock.IsLocked;
    }
}