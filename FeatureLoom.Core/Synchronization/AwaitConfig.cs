using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization;

/// <summary>
/// Provides configurable extension methods for awaiting and synchronously waiting on tasks and value tasks,
/// allowing control over context capturing and exception unwrapping behavior.
/// </summary>
public static class AwaitConfig
{
    /// <summary>
    /// Controls the default behavior for continuations after await when using ConfiguredAwait (not ConfigureAwait).
    /// If false (default), continuations will not attempt to marshal back to the captured context.
    /// </summary>
    public static bool ContinueOnCapturedContextByDefault { get; set; } = false;

    /// <summary>
    /// Controls the default behavior for exception unwrapping in WaitConfigured methods.
    /// If true (default), exceptions are unwrapped and thrown directly; otherwise, they are wrapped in AggregateException.
    /// </summary>
    public static bool UnwrapExceptionsByDefault { get; set; } = true;

    /// <summary>
    /// Awaits the task using the configured default or an explicit override for context capturing.
    /// Note: replace ConfigureAwait with ConfiguredAwait in your code to change default behaviour via ContinueOnCapturedContextByDefault
    /// </summary>
    /// <param name="task">The task to await.</param>
    /// <param name="continueOnCapturedContext">Optional override for context capturing behavior.</param>
    /// <returns>A configured awaitable for the task.</returns>
    public static ConfiguredTaskAwaitable ConfiguredAwait(this Task task, bool? continueOnCapturedContext = null)
    {
        bool useContext = continueOnCapturedContext ?? ContinueOnCapturedContextByDefault;
        return task.ConfigureAwait(useContext);
    }

    /// <summary>
    /// Awaits the task using the configured default or an explicit override for context capturing.
    /// Note: replace ConfigureAwait with ConfiguredAwait in your code to change default behaviour via ContinueOnCapturedContextByDefault
    /// </summary>
    /// <typeparam name="TResult">The result type of the task.</typeparam>
    /// <param name="task">The task to await.</param>
    /// <param name="continueOnCapturedContext">Optional override for context capturing behavior.</param>
    /// <returns>A configured awaitable for the task.</returns>
    public static ConfiguredTaskAwaitable<TResult> ConfiguredAwait<TResult>(this Task<TResult> task, bool? continueOnCapturedContext = null)
    {
        bool useContext = continueOnCapturedContext ?? ContinueOnCapturedContextByDefault;
        return task.ConfigureAwait(useContext);
    }

#if !NETSTANDARD2_0
    /// <summary>
    /// Awaits the ValueTask using the configured default or an explicit override for context capturing.
    /// Note: replace ConfigureAwait with ConfiguredAwait in your code to change default behaviour via ContinueOnCapturedContextByDefault
    /// </summary>
    /// <param name="task">The ValueTask to await.</param>
    /// <param name="continueOnCapturedContext">Optional override for context capturing behavior.</param>
    /// <returns>A configured awaitable for the ValueTask.</returns>
    public static ConfiguredValueTaskAwaitable ConfiguredAwait(this ValueTask task, bool? continueOnCapturedContext = null)
    {
        bool useContext = continueOnCapturedContext ?? ContinueOnCapturedContextByDefault;
        return task.ConfigureAwait(useContext);
    }

    /// <summary>
    /// Awaits the ValueTask using the configured default or an explicit override for context capturing.
    /// Note: replace ConfigureAwait with ConfiguredAwait in your code to change default behaviour via ContinueOnCapturedContextByDefault
    /// </summary>
    /// <typeparam name="T">The result type of the ValueTask.</typeparam>
    /// <param name="task">The ValueTask to await.</param>
    /// <param name="continueOnCapturedContext">Optional override for context capturing behavior.</param>
    /// <returns>A configured awaitable for the ValueTask.</returns>
    public static ConfiguredValueTaskAwaitable<T> ConfiguredAwait<T>(this ValueTask<T> task, bool? continueOnCapturedContext = null)
    {
        bool useContext = continueOnCapturedContext ?? ContinueOnCapturedContextByDefault;
        return task.ConfigureAwait(useContext);
    }
#endif

    /// <summary>
    /// Synchronously waits for the task to complete, using the configured default or an explicit override for exception unwrapping.
    /// </summary>
    /// <param name="task">The task to wait on.</param>
    /// <param name="unwrapException">
    /// If true, throws the original exception directly (default); 
    /// if false, wraps exceptions in AggregateException.
    /// </param>
    public static void WaitConfigured(this Task task, bool? unwrapException = null)
    {
        bool unwrap = unwrapException ?? UnwrapExceptionsByDefault;
        if (unwrap)
        {
            task.GetAwaiter().GetResult();
        }
        else
        {
            task.Wait();
        }
    }

    /// <summary>
    /// Synchronously waits for the task to complete and returns the result, using the configured default or an explicit override for exception unwrapping.
    /// </summary>
    /// <typeparam name="TResult">The result type of the task.</typeparam>
    /// <param name="task">The task to wait on.</param>
    /// <param name="unwrapException">
    /// If true, throws the original exception directly (default); 
    /// if false, wraps exceptions in AggregateException.
    /// </param>
    /// <returns>The result of the completed task.</returns>
    public static TResult WaitConfigured<TResult>(this Task<TResult> task, bool? unwrapException = null)
    {
        bool unwrap = unwrapException ?? UnwrapExceptionsByDefault;
        if (unwrap)
        {
            return task.GetAwaiter().GetResult();
        }
        else
        {
            return task.Result;
        }
    }

#if !NETSTANDARD2_0
    /// <summary>
    /// Synchronously waits for the ValueTask to complete, using the configured default or an explicit override for exception unwrapping.
    /// </summary>
    /// <param name="task">The ValueTask to wait on.</param>
    /// <param name="unwrapException">
    /// If true, throws the original exception directly (default); 
    /// if false, wraps exceptions in AggregateException.
    /// </param>
    public static void WaitConfigured(this ValueTask task, bool? unwrapException = null)
    {
        bool unwrap = unwrapException ?? UnwrapExceptionsByDefault;
        if (unwrap)
        {
            task.GetAwaiter().GetResult();
        }
        else
        {
            task.AsTask().Wait();
        }
    }

    /// <summary>
    /// Synchronously waits for the ValueTask to complete and returns the result, using the configured default or an explicit override for exception unwrapping.
    /// </summary>
    /// <typeparam name="T">The result type of the ValueTask.</typeparam>
    /// <param name="task">The ValueTask to wait on.</param>
    /// <param name="unwrapException">
    /// If true, throws the original exception directly (default); 
    /// if false, wraps exceptions in AggregateException.
    /// </param>
    /// <returns>The result of the completed ValueTask.</returns>
    public static T WaitConfigured<T>(this ValueTask<T> task, bool? unwrapException = null)
    {
        bool unwrap = unwrapException ?? UnwrapExceptionsByDefault;
        if (unwrap)
        {
            return task.GetAwaiter().GetResult();
        }
        else
        {
            return task.AsTask().Result;
        }
    }
#endif
}