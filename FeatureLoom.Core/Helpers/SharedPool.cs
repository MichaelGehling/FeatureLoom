using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Helpers
{
    /// <summary>
    /// Thread-local batched object pool with a shared global reservoir.
    /// <para>
    /// Each thread maintains its own local <see cref="Stack{T}"/> of items to minimize contention.
    /// When a thread-local stack is empty, a batch of items is moved from the global stack (up to <c>fetchOnEmpty</c>).
    /// When a thread-local stack exceeds its configured capacity, surplus items are spilled back to the global stack
    /// or discarded (if the global stack is at capacity).
    /// </para>
    /// <para>   
    /// Decisions made outside locks may race; the implementation intentionally tolerates minor over/under creation
    /// or premature discarding for performance.
    /// </para>
    /// </summary>
    /// <typeparam name="T">
    /// The pooled reference type. If <typeparamref name="T"/> implements <see cref="IDisposable"/>, discarded instances
    /// are automatically disposed unless a custom onDiscard is supplied.
    /// </typeparam>
    public static class SharedPool<T> where T : class
    {
        // Global shared stack guarded by globalLock.
        static Stack<T> globalStack = new Stack<T>();
        static MicroLock globalLock = new MicroLock();

        // Per-thread lazy-initialized stack wrapper.
        [ThreadStatic]
        static LazyValue<Stack<T>> threadLocalStack;

        // Initialization control.
        static MicroLock initLock = new MicroLock();
        volatile static bool initialized = false;

        /// <summary>
        /// Indicates whether the pool has been initialized via <see cref="TryInit"/>.
        /// </summary>
        public static bool IsInitialized => initialized;

        // Configuration & callbacks.
        static Func<T> create;
        static Action<T> reset;
        static Action<T> discard;
        static int globalCapacity;
        static int localCapacity;
        static int fetchOnEmpty;
        static int keepOnFull;

        /// <summary>
        /// Approximate number of items currently held in the global stack (not locked).
        /// </summary>
        public static int GlobalCount => globalStack.Count;

        /// <summary>
        /// Number of items currently held in the calling thread's local stack.
        /// </summary>
        public static int LocalCount => threadLocalStack.ObjIfExists?.Count ?? 0;

        /// <summary>
        /// Configured maximum number of items the global stack will retain.
        /// </summary>
        public static int GlobalCapacity => globalCapacity;

        /// <summary>
        /// Configured soft maximum number of items the local (per-thread) stack should retain.
        /// Surplus is spilled or discarded down to <see cref="keepOnFull"/>.
        /// </summary>
        public static int LocalCapacity => localCapacity;

        /// <summary>
        /// Initializes the pool. Must be called exactly once before any other method.
        /// Subsequent calls return <c>false</c>.
        /// </summary>
        /// <param name="onCreate">Factory delegate to create new instances when needed (never null).</param>
        /// <param name="onReset">
        /// Optional reset action invoked on every returned item (may be null; invoked with null-conditional access).
        /// Should prepare the item for reuse.
        /// </param>
        /// <param name="onDiscard">
        /// Optional discard action invoked when an item is dropped because capacities are exceeded.
        /// If null and <typeparamref name="T"/> implements <see cref="IDisposable"/>, a default disposal action is used.
        /// </param>
        /// <param name="globalCapacity">Maximum number of items retained globally (clamped to ≥ 0).</param>
        /// <param name="localCapacity">Maximum number of items retained per thread (clamped to ≥ 1).</param>
        /// <param name="fetchOnEmpty">
        /// Number of items (at most) prefetched from global into a thread-local stack when it is empty (clamped to [0, localCapacity]).
        /// </param>
        /// <param name="keepOnFull">
        /// Target number of items to keep locally after trimming on overflow (clamped to [0, localCapacity]).
        /// </param>
        /// <returns><c>true</c> if initialization succeeded; <c>false</c> if already initialized.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="onCreate"/> is null.</exception>
        public static bool TryInit(Func<T> onCreate, Action<T> onReset, Action<T> onDiscard = null,
            int globalCapacity = 1000, int localCapacity = 50, int fetchOnEmpty = 40, int keepOnFull = 10)
        {
            if (initialized) return false;

            if (onCreate == null) throw new ArgumentNullException(nameof(onCreate));
            if (onDiscard == null && typeof(IDisposable).IsAssignableFrom(typeof(T)))
            {
                onDiscard = item => ((IDisposable)item).Dispose();
            }            

            using (initLock.Lock())
            {
                if (initialized) return false;

                SharedPool<T>.create = onCreate;
                SharedPool<T>.reset = onReset;
                SharedPool<T>.discard = onDiscard;
                SharedPool<T>.globalCapacity = globalCapacity.ClampLow(0);
                SharedPool<T>.localCapacity = localCapacity.ClampLow(1);
                SharedPool<T>.fetchOnEmpty = fetchOnEmpty.Clamp(0, SharedPool<T>.localCapacity);
                SharedPool<T>.keepOnFull = keepOnFull.Clamp(0, SharedPool<T>.localCapacity);

                initialized = true;
            }
            return true;
        }

        /// <summary>
        /// Obtains an item from the thread-local stack if available; otherwise attempts a batched fetch
        /// from the global stack. If the global stack is empty, a new item is created via the factory.
        /// </summary>
        /// <remarks>
        /// May create a new instance even if another thread concurrently returned items to the global stack
        /// (non-locked emptiness check). This is an accepted trade-off for reduced contention.
        /// </remarks>
        /// <returns>An item ready for use.</returns>
        /// <exception cref="InvalidOperationException">Thrown if called before successful <see cref="TryInit"/>.</exception>
        public static T Take()
        {
            if (!initialized) throw new InvalidOperationException("SharedPool not initialized. Call SharedPool<T>.TryInit(createFunc, resetAction) first.");

            var localStack = threadLocalStack.Obj;
            if (localStack.TryPop(out T item)) return item;

            if (globalStack.Count == 0) return create();

            using (globalLock.Lock())
            {
                if (!globalStack.TryPop(out T reservedItem)) return create();

                // Prefetch batch
                while (localStack.Count < fetchOnEmpty && globalStack.TryPop(out item))
                {
                    localStack.Push(item);
                }
                return reservedItem;
            }
        }

        /// <summary>
        /// Returns an item to the pool, invoking the reset callback (if provided), then pushing to the
        /// thread-local stack. On local overflow, spills excess to the global stack or discards items
        /// down to <see cref="keepOnFull"/>.
        /// </summary>
        /// <remarks>
        /// When global capacity is reached, surplus items are discarded using <see cref="discard"/>.
        /// Discard decision can race with other threads freeing global space.
        /// </remarks>
        /// <param name="item">The item to return; if null, the call is ignored.</param>
        /// <exception cref="InvalidOperationException">Thrown if called before successful <see cref="TryInit"/>.</exception>
        public static void Return(T item)
        {
            if (!initialized) throw new InvalidOperationException("SharedPool not initialized. Call SharedPool<T>.TryInit(createFunc, resetAction) first.");
            if (item == null) return;

            reset?.Invoke(item);

            var localStack = threadLocalStack.Obj;
            localStack.Push(item);
            if (localStack.Count <= localCapacity) return;

            // Fast discard path (approximate global fullness)
            if (globalStack.Count >= globalCapacity)
            {
                while (localStack.Count > keepOnFull)
                {
                    item = localStack.Pop();
                    discard?.Invoke(item);
                }
                return;
            }

            // Spill under lock
            using (globalLock.Lock())
            {
                while (localStack.Count > keepOnFull)
                {
                    item = localStack.Pop();
                    if (globalStack.Count < globalCapacity)
                    {
                        globalStack.Push(item);
                    }
                    else
                    {
                        discard?.Invoke(item);
                    }
                }
            }
        }

        /// <summary>
        /// Clears (and discards) all items currently held in the calling thread's local stack.
        /// Does nothing if the thread-local stack was never created.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if called before successful <see cref="TryInit"/>.</exception>
        public static void ClearLocal()
        {
            if (!initialized) throw new InvalidOperationException("SharedPool not initialized. Call SharedPool<T>.TryInit(createFunc, resetAction) first.");
            if (!threadLocalStack.Exists) return;

            var localStack = threadLocalStack.Obj;
            while (localStack.Count > 0)
            {
                var item = localStack.Pop();
                discard?.Invoke(item);
            }
        }

        /// <summary>
        /// Clears (and discards) all items currently held in the global stack.
        /// A racy empty check avoids locking when obviously empty.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if called before successful <see cref="TryInit"/>.</exception>
        public static void ClearGlobal()
        {
            if (!initialized) throw new InvalidOperationException("SharedPool not initialized. Call SharedPool<T>.TryInit(createFunc, resetAction) first.");

            if (globalStack.Count == 0) return;

            using (globalLock.Lock())
            {
                while (globalStack.Count > 0)
                {
                    var item = globalStack.Pop();
                    discard?.Invoke(item);
                }
            }
        }
    }
}
