using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Synchronization
{
    /// <summary>
    /// A class-based wrapper around <see cref="MicroValueLock"/> that exposes a disposable
    /// <see cref="LockHandle"/>, enabling the <c>using</c> pattern for both write and read locks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Because <see cref="MicroLock"/> is a class, it can be stored in fields, passed by reference,
    /// and shared freely without the copy hazards of <see cref="MicroValueLock"/> directly.
    /// </para>
    /// <para>
    /// <b>Do not hold a lock across <c>await</c> points.</b> <see cref="LockHandle"/> is a regular
    /// struct (not a <c>ref struct</c>), so the compiler will not prevent it from being used in async
    /// contexts. Holding the lock across an <c>await</c> is a deadlock risk and defeats the purpose
    /// of a micro-lock.
    /// </para>
    /// </remarks>
    public sealed class MicroLock
    {
        private MicroValueLock valueLock;

        /// <summary>
        /// Gets a value indicating whether the lock is currently held by any reader or writer.
        /// This is a non-atomic snapshot and should be used for diagnostics only.
        /// </summary>
        public bool IsLocked => valueLock.IsLocked;

        /// <summary>
        /// Acquires the write lock, yielding the thread until it becomes available,
        /// and returns a <see cref="LockHandle"/> that releases the lock when disposed.
        /// </summary>
        /// <param name="prioritized">
        /// When <c>true</c> the thread signals to readers that a writer is waiting, preventing
        /// writer starvation. See <see cref="MicroValueLock.Enter(bool)"/>.
        /// </param>
        /// <returns>A <see cref="LockHandle"/> that must be disposed to release the write lock.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle Lock(bool prioritized = false)
        {
            valueLock.Enter(prioritized);
            return new LockHandle(this, false);
        }

        /// <summary>
        /// Attempts to acquire the write lock without blocking.
        /// </summary>
        /// <param name="lockHandle">
        /// When this method returns <c>true</c>, contains an active <see cref="LockHandle"/> that
        /// must be disposed to release the lock. When <c>false</c>, contains an inactive handle.
        /// </param>
        /// <param name="prioritized">See <see cref="Lock(bool)"/>.</param>
        /// <returns><c>true</c> if the write lock was acquired; otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out LockHandle lockHandle, bool prioritized = false)
        {
            if (valueLock.TryEnter(prioritized))
            {
                lockHandle = new LockHandle(this, false);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        /// <summary>
        /// Attempts to acquire the write lock, yielding the thread until the lock becomes available
        /// or the specified timeout elapses.
        /// </summary>
        /// <param name="lockHandle">See <see cref="TryLock(out LockHandle, bool)"/>.</param>
        /// <param name="timeout">The maximum time to wait for the write lock.</param>
        /// <param name="prioritized">See <see cref="Lock(bool)"/>.</param>
        /// <returns><c>true</c> if the write lock was acquired within the timeout; otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out LockHandle lockHandle, TimeSpan timeout, bool prioritized = false)
        {
            if (valueLock.TryEnter(timeout, prioritized))
            {
                lockHandle = new LockHandle(this, false);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        /// <summary>
        /// Acquires a read lock, yielding the thread until one becomes available,
        /// and returns a <see cref="LockHandle"/> that releases the lock when disposed.
        /// Multiple readers may hold the read lock simultaneously.
        /// </summary>
        /// <param name="prioritized">See <see cref="MicroValueLock.TryEnterReadOnly(bool)"/>.</param>
        /// <returns>A <see cref="LockHandle"/> that must be disposed to release the read lock.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle LockReadOnly(bool prioritized = false)
        {
            valueLock.EnterReadOnly(prioritized);
            return new LockHandle(this, true);
        }

        /// <summary>
        /// Attempts to acquire a read lock without blocking.
        /// </summary>
        /// <param name="lockHandle">See <see cref="TryLock(out LockHandle, bool)"/>.</param>
        /// <param name="prioritized">See <see cref="MicroValueLock.TryEnterReadOnly(bool)"/>.</param>
        /// <returns><c>true</c> if a read lock was acquired; otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out LockHandle lockHandle, bool prioritized = false)
        {
            if (valueLock.TryEnterReadOnly(prioritized))
            {
                lockHandle = new LockHandle(this, true);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        /// <summary>
        /// Attempts to acquire a read lock, yielding the thread until one becomes available or
        /// the specified timeout elapses.
        /// </summary>
        /// <param name="lockHandle">See <see cref="TryLock(out LockHandle, bool)"/>.</param>
        /// <param name="timeout">The maximum time to wait for a read lock.</param>
        /// <param name="prioritized">See <see cref="MicroValueLock.TryEnterReadOnly(bool)"/>.</param>
        /// <returns><c>true</c> if a read lock was acquired within the timeout; otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out LockHandle lockHandle, TimeSpan timeout, bool prioritized = false)
        {
            if (valueLock.TryEnterReadOnly(timeout, prioritized))
            {
                lockHandle = new LockHandle(this, true);
                return true;
            }
            else
            {
                lockHandle = new LockHandle();
                return false;
            }
        }

        /// <summary>
        /// A disposable handle representing an acquired write or read lock on a <see cref="MicroLock"/>.
        /// Dispose (or call <see cref="Exit"/> explicitly) to release the lock.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Because this is a struct, copying a <see cref="LockHandle"/> produces two handles that
        /// both reference the same underlying lock. Disposing either copy releases the lock; disposing
        /// the second copy is a no-op. Avoid copying handles — use them directly in a <c>using</c>
        /// statement or pass by <c>ref</c>.
        /// </para>
        /// <para>
        /// <see cref="Exit"/> and <see cref="Dispose"/> are idempotent: calling either more than
        /// once on the same instance is safe and has no effect after the first call.
        /// </para>
        /// </remarks>
        public struct LockHandle : IDisposable
        {
            private MicroLock parentLock;
            private readonly bool readOnly;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal LockHandle(MicroLock parentLock, bool readOnly)
            {
                this.parentLock = parentLock;
                this.readOnly = readOnly;
            }

            /// <inheritdoc cref="Exit"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => Exit();

            /// <summary>
            /// Releases the lock. Safe to call multiple times; subsequent calls are no-ops.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                if (parentLock == null) return;
                if (readOnly) parentLock.valueLock.ExitReadOnly();
                else parentLock.valueLock.Exit();
                parentLock = null;
            }

            /// <summary>
            /// Gets a value indicating whether the lock is still held by this handle.
            /// Returns <c>false</c> after <see cref="Exit"/> or <see cref="Dispose"/> has been called.
            /// </summary>
            public bool IsActive => parentLock != null;
        }
    }
}