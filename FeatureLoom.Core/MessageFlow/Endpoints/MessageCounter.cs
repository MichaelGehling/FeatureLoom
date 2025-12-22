using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Counts messages posted to this sink and allows awaiting until the counter reaches a specified value.
/// </summary>
/// <remarks>
/// Thread-safe via <see cref="FeatureLock"/>. Waiters are stored in descending order of expected count.
/// Completions are performed outside the lock to reduce contention and avoid running continuations under the lock.
/// </remarks>
public sealed class MessageCounter : IMessageSink
{
    private long counter;
    private readonly FeatureLock myLock = new FeatureLock();
    private readonly List<(long expectedCount, TaskCompletionSource<long> tcs)> waitings = new List<(long, TaskCompletionSource<long>)>();

    /// <summary>
    /// Gets the current message count.
    /// </summary>
    public long Counter
    {
        get
        {
            using (myLock.LockReadOnly())
            {
                return counter;
            }
        }
    }

    /// <summary>
    /// Returns a task that completes when the counter reaches at least <paramref name="numMessages"/>.
    /// </summary>
    /// <param name="numMessages">The count threshold to wait for.</param>
    /// <param name="relative"> If true, <paramref name="numMessages"/> is relative to the current count; otherwise, it is absolute.</param>
    /// <returns>A task that completes with the current count once the threshold is reached.</returns>
    /// <remarks>
    /// If <paramref name="numMessages"/> is less than or equal to 0, the task is completed immediately.
    /// If the threshold is already met, the task completes immediately.
    /// Otherwise, a waiter is enqueued (sorted descending by expected count).
    /// </remarks>
    public Task<long> WaitForCountAsync(long numMessages, bool relative = false)
    {
        if (numMessages <= 0) return Task.FromResult(Counter);

        using (myLock.Lock())
        {
            var currentCounter = counter;
            if (relative)
            {
                numMessages += currentCounter;
            }
            var waitingTaskSource = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (currentCounter >= numMessages)
            {
                waitingTaskSource.SetResult(currentCounter);
            }
            else
            {
                waitings.Add((numMessages, waitingTaskSource));
                // Keep descending order (largest expectedCount first)
                waitings.Sort((a, b) => b.expectedCount.CompareTo(a.expectedCount));
            }

            return waitingTaskSource.Task;
        }
    }

    /// <summary>
    /// Posts a message (by readonly reference) and increments the counter.
    /// </summary>
    public void Post<M>(in M message) => Count();

    /// <summary>
    /// Posts a message and increments the counter.
    /// </summary>
    public void Post<M>(M message) => Count();

    /// <summary>
    /// Asynchronously posts a message and increments the counter.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task PostAsync<M>(M message)
    {
        Count();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Increments the counter and completes any waiters whose expected count is met.
    /// Completions occur outside the lock.
    /// </summary>
    private void Count()
    {
        // Fast-path: single waiter completed (avoid array allocation entirely)
        TaskCompletionSource<long> singleTcs = null;

        // Batch-path: multiple completions
        TaskCompletionSource<long>[] toCompleteArray = null;
        int toCompleteCount = 0;
        long newCount;

        using (myLock.Lock())
        {
            counter++;
            newCount = counter;

            // Determine number of satisfied waiters (from end due to descending sort)
            for (int i = waitings.Count - 1; i >= 0; i--)
            {
                if (waitings[i].expectedCount <= newCount) toCompleteCount++;
                else break;
            }

            if (toCompleteCount == 0) return;

            if (toCompleteCount == 1)
            {
                // Single item: capture TCS directly, remove from list, no allocations
                singleTcs = waitings[waitings.Count - 1].tcs;
                waitings.RemoveAt(waitings.Count - 1);
            }
            else
            {
                toCompleteArray = new TaskCompletionSource<long>[toCompleteCount];
                for (int i = 0; i < toCompleteCount; i++)
                {
                    toCompleteArray[i] = waitings[waitings.Count - 1 - i].tcs;
                }

                waitings.RemoveRange(waitings.Count - toCompleteCount, toCompleteCount);
            }
        }

        // Complete outside the lock
        if (singleTcs != null)
        {
            singleTcs.SetResult(newCount);
            return;
        }

        if (toCompleteArray != null)
        {
            for (int i = 0; i < toCompleteCount; i++)
            {
                toCompleteArray[i].SetResult(newCount);
            }
        }
    }
}