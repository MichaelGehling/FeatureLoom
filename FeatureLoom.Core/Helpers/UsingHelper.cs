using System;

namespace FeatureLoom.Helpers
{
    /// <summary>
    /// Lightweight helper that executes an optional <paramref name="before"/> action immediately
    /// and an optional <paramref name="after"/> action when disposed (e.g. by a using statement).
    /// </summary>
    /// <remarks>
    /// Typical usage:
    /// <code>
    /// using (UsingHelper.Do(
    ///     before: () => AcquireResource(),
    ///     after: () => ReleaseResource()))
    /// {
    ///     // Work with the resource
    /// }
    /// </code>
    /// The <c>before</c> action (if provided) is invoked in the constructor, so any exception it throws
    /// happens before a disposable instance is returned. The <c>after</c> action (if provided) is invoked
    /// exactly once when <see cref="Dispose"/> is called.
    /// 
    /// Because this is a readonly struct holding only a delegate reference, allocation overhead is minimal.
    /// Avoid copying the struct and disposing multiple copies manually; doing so would invoke the <c>after</c>
    /// action multiple times. Use it in a <c>using</c> statement or as a local variable only.
    /// </remarks>
    public readonly struct UsingHelper : IDisposable
    {
        // Action to execute on Dispose (may be null).
        private readonly Action after;

        /// <summary>
        /// Creates a new <see cref="UsingHelper"/>, invoking the optional <paramref name="before"/> action immediately.
        /// </summary>
        /// <param name="before">Action executed immediately (can be null).</param>
        /// <param name="after">Action executed on <see cref="Dispose"/> (can be null).</param>
        public UsingHelper(Action before, Action after)
        {
            before?.Invoke();
            this.after = after;
        }

        /// <summary>
        /// Invokes the stored <c>after</c> action if present.
        /// </summary>
        public void Dispose()
        {
            after?.Invoke();
        }

        /// <summary>
        /// Convenience factory. Executes <paramref name="before"/> now and schedules <paramref name="after"/> for disposal.
        /// </summary>
        /// <param name="before">Action executed immediately (can be null).</param>
        /// <param name="after">Action executed on disposal (can be null).</param>
        /// <returns>A configured <see cref="UsingHelper"/>.</returns>
        public static UsingHelper Do(Action before, Action after) => new UsingHelper(before, after);

        /// <summary>
        /// Convenience factory for only an <paramref name="after"/> action.
        /// </summary>
        /// <param name="after">Action executed on disposal (can be null).</param>
        /// <returns>A configured <see cref="UsingHelper"/>.</returns>
        public static UsingHelper Do(Action after) => new UsingHelper(null, after);
    }
}