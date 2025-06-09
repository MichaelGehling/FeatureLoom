using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Collections;

/// <summary>
/// A thread-safe circular buffer for storing log entries with unique, incrementing IDs.
/// When the buffer is full, new entries overwrite the oldest ones.
/// </summary>
/// <typeparam name="T">Type of items to store in the buffer.</typeparam>
public sealed class CircularLogBuffer<T> : ILogBuffer<T>
{
    private T[] buffer;
    private int nextIndex = 0; // Index where the next item will be written
    private long counter = 0; // Total number of items ever added (used for IDs)
    private bool cycled = false; // Indicates if the buffer has wrapped around at least once
    private LazyValue<AsyncManualResetEvent> newEntryEvent = new();
    private MicroValueLock myLock;
    private bool threadSafe = true;

    /// <summary>
    /// Initializes a new instance of the CircularLogBuffer class.
    /// </summary>
    /// <param name="bufferSize">The maximum number of items the buffer can hold.</param>
    /// <param name="threadSafe">Whether to enable thread safety (default: true).</param>
    public CircularLogBuffer(int bufferSize, bool threadSafe = true)
    {
        buffer = new T[bufferSize];
        this.threadSafe = threadSafe;
    }

    /// <summary>
    /// Gets the current number of items in the buffer.
    /// </summary>
    public int CurrentSize => cycled ? buffer.Length : nextIndex;

    /// <summary>
    /// Gets the maximum number of items the buffer can hold.
    /// </summary>
    public int MaxSize => buffer.Length;

    /// <summary>
    /// Gets the ID of the most recently added item.
    /// </summary>
    public long LatestId => counter - 1;

    /// <summary>
    /// Gets the ID of the oldest available item in the buffer.
    /// </summary>
    public long OldestAvailableId => LatestId - CurrentSize;

    /// <summary>
    /// Gets an async wait handle that is signaled when a new entry is added.
    /// </summary>
    public IAsyncWaitHandle WaitHandle
    {
        get
        {
            return newEntryEvent.Obj;
        }
    }

    /// <summary>
    /// Adds an item to the buffer and returns its assigned ID.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>The ID assigned to the added item.</returns>
    public long Add(T item)
    {
        long result;
        if (threadSafe) myLock.Enter(true);
        try
        {
            buffer[nextIndex++] = item;
            if (nextIndex >= buffer.Length)
            {
                nextIndex = 0;
                cycled = true;
            }
            result = counter++;
        }
        finally
        {
            if (threadSafe) myLock.Exit();
        }

        newEntryEvent.ObjIfExists?.PulseAll();
        return result;
    }

    /// <summary>
    /// Adds a range of items to the buffer.
    /// </summary>
    /// <typeparam name="IEnum">Type of the enumerable collection.</typeparam>
    /// <param name="items">The items to add.</param>
    public void AddRange<IEnum>(IEnum items) where IEnum : IEnumerable<T>
    {
        if (threadSafe) myLock.Enter(true);
        try
        {
            foreach (var item in items)
            {
                buffer[nextIndex++] = item;
                if (nextIndex >= buffer.Length)
                {
                    nextIndex = 0;
                    cycled = true;
                }
                counter++;
            }
        }
        finally
        {
            if (threadSafe) myLock.Exit();
        }

        newEntryEvent.ObjIfExists?.PulseAll();
    }

    /// <summary>
    /// Resets the buffer, removing all items and resetting IDs.
    /// </summary>
    public void Reset()
    {
        if (threadSafe) myLock.Enter(true);
        try
        {
            cycled = false;
            counter = 0;
            nextIndex = 0;
        }
        finally
        {
            if (threadSafe) myLock.Exit();
        }
    }

    /// <summary>
    /// Checks if the buffer contains the specified item.
    /// </summary>
    /// <param name="item">The item to search for.</param>
    /// <returns>True if the item is found; otherwise, false.</returns>
    public bool Contains(T item)
    {
        if (threadSafe) myLock.EnterReadOnly(true);
        try
        {
            int until = cycled ? buffer.Length : nextIndex;
            for (int i = 0; i < until; i++)
            {
                if (buffer[i]?.Equals(item) ?? buffer[i] == null && item == null) return true;
            }
            return false;
        }
        finally
        {
            if (threadSafe) myLock.ExitReadOnly();
        }
    }

    /// <summary>
    /// Gets the most recently added item in the buffer.
    /// </summary>
    /// <returns>The latest item.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the buffer is empty.</exception>
    public T GetLatest()
    {
        if (threadSafe) myLock.EnterReadOnly(true);
        try
        {
            if (nextIndex == 0)
            {
                if (cycled) return buffer[buffer.Length - 1];
                else throw new ArgumentOutOfRangeException();
            }
            else return buffer[nextIndex - 1];
        }
        finally
        {
            if (threadSafe) myLock.ExitReadOnly();
        }
    }

    /// <summary>
    /// Tries to get an item by its assigned ID.
    /// </summary>
    /// <param name="number">The ID of the item to retrieve.</param>
    /// <param name="result">The retrieved item, if found.</param>
    /// <returns>True if the item was found; otherwise, false.</returns>
    public bool TryGetFromId(long number, out T result)
    {
        if (threadSafe) myLock.EnterReadOnly(true);
        try
        {
            result = default;
            if (number > LatestId || number < OldestAvailableId) return false;

            int offset = (int)(counter - number);
            int index = (nextIndex - offset + buffer.Length) % buffer.Length;
            result = buffer[index];
            return true;
        }
        finally
        {
            if (threadSafe) myLock.ExitReadOnly();
        }
    }

    /// <summary>
    /// Gets all available items starting from a requested ID.
    /// </summary>
    /// <param name="firstRequestedId">The first requested ID.</param>
    /// <param name="firstProvidedId">The first ID actually provided.</param>
    /// <param name="lastProvidedId">The last ID actually provided.</param>
    /// <returns>An array of available items.</returns>
    public T[] GetAllAvailable(long firstRequestedId, out long firstProvidedId, out long lastProvidedId) => GetAllAvailable(firstRequestedId, buffer.Length, out firstProvidedId, out lastProvidedId);

    /// <summary>
    /// Gets up to a maximum number of available items starting from a requested ID.
    /// </summary>
    /// <param name="firstRequestedId">The first requested ID.</param>
    /// <param name="maxItems">The maximum number of items to return.</param>
    /// <param name="firstProvidedId">The first ID actually provided.</param>
    /// <param name="lastProvidedId">The last ID actually provided.</param>
    /// <returns>An array of available items.</returns>
    public T[] GetAllAvailable(long firstRequestedId, int maxItems, out long firstProvidedId, out long lastProvidedId)
    {
        if (firstRequestedId >= counter)
        {
            firstProvidedId = -1;
            lastProvidedId = -1;
            return Array.Empty<T>();
        }

        if (threadSafe) myLock.EnterReadOnly(true);
        try
        {
            if (firstRequestedId >= counter)
            {
                firstProvidedId = -1;
                lastProvidedId = -1;
                return Array.Empty<T>();
            }

            if (firstRequestedId < OldestAvailableId) firstProvidedId = OldestAvailableId;
            else firstProvidedId = firstRequestedId;

            int numberToCopy = (int)(counter - firstRequestedId).ClampHigh(maxItems).ClampHigh(CurrentSize);
            lastProvidedId = firstRequestedId + numberToCopy;

            T[] result = new T[numberToCopy];
            CopyToInternal(result, 0, numberToCopy);
            return result;
        }
        finally
        {
            if (threadSafe) myLock.ExitReadOnly();
        }
    }

    /// <summary>
    /// Asynchronously waits until the buffer contains an item with the specified ID.
    /// </summary>
    /// <param name="number">The ID to wait for.</param>
    /// <param name="ct">A cancellation token to observe while waiting.</param>
    /// <returns>A task that completes when the item is available or the operation is cancelled.</returns>
    public async Task WaitForIdAsync(long number, CancellationToken ct = default)
    {
        while (number > LatestId && !ct.IsCancellationRequested)
        {
            if (!myLock.IsLocked) await newEntryEvent.Obj.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Copies the contents of the buffer to an array, starting at the specified array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (threadSafe) myLock.EnterReadOnly(true);
        try
        {
            var leftSpace = array.Length - arrayIndex;
            CopyToInternal(array, arrayIndex, leftSpace > CurrentSize ? CurrentSize : leftSpace);
        }
        finally
        {
            if (threadSafe) myLock.ExitReadOnly();
        }
    }

    /// <summary>
    /// Copies a specified number of items from the buffer to an array, starting at the specified array index.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins.</param>
    /// <param name="copyLength">The number of items to copy.</param>
    public void CopyTo(T[] array, int arrayIndex, int copyLength)
    {
        if (threadSafe) myLock.EnterReadOnly(true);
        try
        {
            CopyToInternal(array, arrayIndex, copyLength);
        }
        finally
        {
            if (threadSafe) myLock.ExitReadOnly();
        }
    }

    /// <summary>
    /// Internal helper to copy items from the buffer to an array, handling wrap-around logic.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The starting index in the destination array.</param>
    /// <param name="copyLength">The number of items to copy.</param>
    private void CopyToInternal(T[] array, int arrayIndex, int copyLength)
    {
        // Calculate how much space is left in the destination array from the starting index.
        var leftSpace = array.Length - arrayIndex;
        // Validate that there is enough space in the destination array and that we don't try to copy more than available.
        if (leftSpace < copyLength || copyLength > CurrentSize) throw new ArgumentOutOfRangeException();

        // The buffer is logically split into two parts:
        // - The "back buffer" (oldest items, after wrap-around)
        // - The "front buffer" (newest items, before wrap-around)
        int frontBufferSize = nextIndex; // Number of items in the front buffer (from index 0 to nextIndex-1)
        int backBufferSize = CurrentSize - nextIndex; // Number of items in the back buffer (from nextIndex to end)

        // Determine how many items to copy from the front buffer (newest items).
        // If we want more than the front buffer holds, we take all of it; otherwise, just what we need.
        int copyFromFrontBuffer = copyLength >= frontBufferSize ? frontBufferSize : copyLength;
        // The starting index in the front buffer for copying.
        int frontBufferStartIndex = frontBufferSize - copyFromFrontBuffer;

        // The remaining items to copy must come from the back buffer (oldest items).
        int copyFromBackbuffer = copyLength - copyFromFrontBuffer;
        // The starting index in the back buffer for copying.
        int backBufferStartIndex = nextIndex + backBufferSize - copyFromBackbuffer;

        // If we need to copy from the back buffer (i.e., the buffer has wrapped around), do so first.
        if (copyFromBackbuffer > 0)
            Array.Copy(buffer, backBufferStartIndex, array, arrayIndex, copyFromBackbuffer);

        // Then copy from the front buffer (the most recent items).
        Array.Copy(buffer, frontBufferStartIndex, array, arrayIndex + copyFromBackbuffer, copyFromFrontBuffer);
    }
}