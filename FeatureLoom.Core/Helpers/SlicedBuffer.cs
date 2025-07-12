using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides efficient allocation and management of array slices from a reusable buffer.
/// <para>
/// <b>How it works:</b><br/>
/// - Uses a single shared buffer to serve multiple array slices, significantly reducing garbage collection (GC) pressure compared to allocating new arrays for each slice.<br/>
/// - When a new slice is requested, it is allocated from the current buffer if there is enough space. If not, the buffer is renewed and optionally grown.
///   The old buffer can be manually reset and reused, or will be discarded. Note: if even a single slice is still in use, the whole buffer remains in memory,
///   so too big buffers can be harmful.<br/>
/// - If the latest allocated slice is no longer needed, its buffer space can be reclaimed and reused for subsequent slices, further improving memory efficiency.<br/>
/// - Slices can only be effectively "returned" (i.e., their space reclaimed) if they are the most recently allocated slice. For this reason, SlicedBuffer does not implement a classic pool pattern.
/// </para>
/// <para>
/// <b>Slice Limit:</b><br/>
/// - The maximum size of a slice that can be allocated from the buffer is determined by <c>sliceLimit</c>.
/// - If <c>growSliceLimit</c> is true, <c>sliceLimit</c> grows with the buffer size; otherwise, it remains fixed based on the initial capacity.
/// </para>
/// <para>
/// <b>When/Why it is helpful:</b><br/>
/// - Ideal for scenarios with many short-lived or temporary array allocations, such as parsing, serialization, or message processing.<br/>
/// - Reduces memory allocations and GC overhead by reusing buffer space and the number of objects on the heap.<br/>
/// - Especially useful when the allocation and release order of slices is predictable (LIFO), allowing efficient buffer space reuse.
/// </para>
/// <para>
/// <b>Size and Heap Allocation Behavior:</b><br/>
/// - The <see cref="SlicedBuffer{T}"/> object itself is always a small object and is allocated on the Small Object Heap (SOH).
/// - The internal buffer (<c>T[] buffer</c>) is a separate array object. Its size determines whether it is allocated on the SOH or the Large Object Heap (LOH).
/// - In .NET, arrays of 85,000 bytes or more are allocated on the LOH, which can lead to increased memory fragmentation and less frequent garbage collection.
/// - By default, <c>maxCapacity</c> is set so that the buffer size stays below 85,000 bytes, ensuring the array remains on the SOH for any type <c>T</c>.
/// - If you explicitly set <c>maxCapacity</c> to a value that results in an array of 85,000 bytes or more, the buffer will be allocated on the LOH.
/// </para>
/// </summary>
public class SlicedBuffer<T>
{
    [ThreadStatic]
    static LazyValue<SlicedBuffer<T>> shared;
    public static SlicedBuffer<T> Shared => shared.Obj;

    T[] buffer;
    int capacity;
    int position;
    int sliceLimit;    
    readonly int initCapacity;        
    readonly int maxCapacity;
    MicroValueLock bufferLock = new();
    readonly bool threadSafe;    
    readonly bool growSliceLimit;
    readonly int minSlicesPerBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlicedBuffer{T}"/> class with default settings.
    /// By default, the buffer is thread-safe, starts with a capacity of 1024 elements, and a max capacity that ensures the internal buffer stays below the Large Object Heap (LOH) threshold (85,000 bytes).
    /// This helps avoid LOH allocations and their associated GC costs. The minimum number of slices per buffer is 4, and sliceLimit growth is enabled, that means the maximum slice size grows with the buffer size.
    /// </summary>
    public SlicedBuffer()
    {
        int maxElements = 84999 / Unsafe.SizeOf<T>();
        this.capacity = 1024.ClampHigh(maxElements);
        this.initCapacity = this.capacity;
        this.buffer = new T[capacity];
        this.position = 0;        
        this.maxCapacity = maxElements;
        this.threadSafe = true;
        this.minSlicesPerBuffer = 4;
        this.growSliceLimit = true;
        this.sliceLimit = initCapacity / minSlicesPerBuffer;        
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="SlicedBuffer{T}"/> class with custom settings.
    /// <para>
    /// <b>Heap allocation note:</b> If <paramref name="maxCapacity"/> is not specified or is less than or equal to zero, it is set so that the internal buffer stays below 84,999 bytes,
    /// avoiding LOH allocation for value types. If you specify a larger <paramref name="maxCapacity"/>, the buffer may be allocated on the LOH.
    /// </para>
    /// </summary>
    /// <param name="capacity">Initial buffer capacity (minimum 64 elements).</param>
    /// <param name="maxCapacity">Maximum buffer capacity (minimum <paramref name="capacity"/>). If not specified (or <= 0), defaults to a value that keeps the buffer below the LOH threshold.</param>
    /// <param name="minSlicesPerBuffer">Minimum number of slices per buffer (clamped between 2 and initCapacity/8).</param>
    /// <param name="growSliceLimit">If true, the maximum slice size grows with the buffer; otherwise, it remains fixed.</param>
    /// <param name="threadSafe">If true, enables thread safety for all buffer operations.</param>
    public SlicedBuffer(int capacity, int maxCapacity = 0, int minSlicesPerBuffer = 4, bool growSliceLimit = false, bool threadSafe = false)
    {
        this.capacity = capacity.ClampLow(64);
        this.initCapacity = this.capacity;
        this.buffer = new T[capacity];
        this.position = 0;
        if (maxCapacity <= 0) 
        {
            int maxElements = 84999 / Unsafe.SizeOf<T>();
            this.maxCapacity = maxElements.ClampLow(capacity);
        }
        else
        {
            this.maxCapacity = maxCapacity.ClampLow(capacity);
        }
        this.threadSafe = threadSafe;
        this.minSlicesPerBuffer = minSlicesPerBuffer.Clamp(2, initCapacity / 8);
        this.sliceLimit = initCapacity / minSlicesPerBuffer;
        this.growSliceLimit = growSliceLimit;
    }

    /// <summary>
    /// Reinitializes the buffer to the current capacity and resets the position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RenewBuffer()
    {        
        var newBuffer = new T[capacity];
        buffer = newBuffer;
        this.position = 0;      
    }

    /// <summary>
    /// Allocates a slice of the specified size from the buffer.
    /// If the buffer does not have enough space and the requested size is less than or equal to the sliceLimit,
    /// the buffer is renewed and grown if configured so. Otherwise, a new array is allocated.
    /// </summary>
    /// <param name="size">The size of the slice to allocate.</param>
    /// <returns>An <see cref="ArraySegment{T}"/> representing the allocated slice.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<T> GetSlice(int size)
    {
        if (!threadSafe) return GetSliceUnsafe(size);

        int leftCapacity = buffer.Length - position;
        if (size > sliceLimit)
        {
            return new ArraySegment<T>(new T[size]);
        }
        else if (size <= leftCapacity)
        {
            if (threadSafe) bufferLock.Enter();
            var slice = new ArraySegment<T>(buffer, position, size);
            if (growSliceLimit) sliceLimit = capacity / minSlicesPerBuffer;
            position += size;
            if (threadSafe) bufferLock.Exit();
            return slice;
        }        
        else
        {
            if (threadSafe) bufferLock.Enter();
            capacity = (capacity + initCapacity).ClampHigh(maxCapacity);
            try
            {
                RenewBuffer();
            }
            catch
            {
                if (threadSafe) bufferLock.Exit();
                throw;
            }
            var slice = new ArraySegment<T>(buffer, position, size);
            position += size;
            if (threadSafe) bufferLock.Exit();
            return slice;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ArraySegment<T> GetSliceUnsafe(int size)
    {
        int leftCapacity = buffer.Length - position;
        if (size > sliceLimit)
        {
            return new ArraySegment<T>(new T[size]);
        }
        else if (size <= leftCapacity)
        {
            var slice = new ArraySegment<T>(buffer, position, size);
            position += size;
            return slice;
        }        
        else
        {
            capacity = (capacity + initCapacity).ClampHigh(maxCapacity);
            if (growSliceLimit) sliceLimit = capacity / minSlicesPerBuffer;
            RenewBuffer();
            var slice = new ArraySegment<T>(buffer, position, size);
            position += size;
            return slice;
        }
    }

    /// <summary>
    /// Resets the buffer position or renews the buffer, optionally resetting the capacity.
    /// </summary>
    /// <param name="reuseExistingBuffer">If true, resets the position to zero without allocating a new buffer.</param>
    /// <param name="resetCapacity">If true and <paramref name="reuseExistingBuffer"/> is false, resets the buffer capacity to its initial value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(bool reuseExistingBuffer, bool resetCapacity = false)
    {
        if (threadSafe) bufferLock.Enter();
        if (reuseExistingBuffer) position = 0;
        else
        {
            if (resetCapacity)
            {
                capacity = initCapacity;
                if (growSliceLimit) sliceLimit = capacity / minSlicesPerBuffer;
            }
            try
            {
                RenewBuffer();
            }
            catch
            {
                if (threadSafe) bufferLock.Exit();
                throw;
            }
        }
        if (threadSafe) bufferLock.Exit();
    }

    /// <summary>
    /// Extends the specified slice by the given number of elements.
    /// If the slice is the latest allocated from the buffer and there is enough capacity, the slice is extended in place.
    /// Otherwise, a new slice is allocated and the data is copied.
    /// <para>
    /// <b>Warning:</b> After calling this method, any previous copies of the original <see cref="ArraySegment{T}"/> must not be used,
    /// as they may refer to outdated or invalid buffer regions.
    /// </para>
    /// </summary>
    /// <param name="slice">The slice to extend (passed by reference and updated).</param>
    /// <param name="additionalElements">The number of elements to add to the slice.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExtendSlice(ref ArraySegment<T> slice, int additionalElements)
    {
        ResizeSlice(ref slice, slice.Count + additionalElements);
    }

    /// <summary>
    /// Frees the buffer space used by the specified slice if it is the latest allocated slice.
    /// If the slice is not the latest, this method has no effect.
    /// <para>
    /// <b>Warning:</b> After calling this method, any previous copies of the original <see cref="ArraySegment{T}"/> must not be used,
    /// as they may refer to outdated or invalid buffer regions.
    /// </para>
    /// </summary>
    /// <param name="slice">The slice to free (passed by reference and updated).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FreeSlice(ref ArraySegment<T> slice)
    {
        ResizeSlice(ref slice, 0);
    }

    /// <summary>
    /// Resizes the specified slice to a new size. If the slice is the latest allocated slice,
    /// buffer space is reclaimed or extended as needed. Otherwise, a new slice is created if growing,
    /// or a new segment is returned if shrinking.
    /// <para>
    /// <b>Warning:</b> After calling this method, any previous copies of the original <see cref="ArraySegment{T}"/> must not be used,
    /// as they may refer to outdated or invalid buffer regions.
    /// </para>
    /// </summary>
    /// <param name="slice">The slice to resize (passed by reference and updated).</param>
    /// <param name="newSize">The new size of the slice.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResizeSlice(ref ArraySegment<T> slice, int newSize)
    {
        newSize = newSize.ClampLow(0);
        if (slice.Count == newSize) return;
        
        if (threadSafe) bufferLock.Enter();
        if (newSize > slice.Count)
        {
            int additionalElements = newSize - slice.Count;

            int leftCapacity = buffer.Length - position;
            if (slice.Array == this.buffer &&
               slice.Offset + slice.Count == this.position &&
               leftCapacity >= additionalElements)
            {
                position += additionalElements;
                slice = new ArraySegment<T>(buffer, slice.Offset, slice.Count + additionalElements);
            }
            else
            {
                var newSlice = GetSliceUnsafe(slice.Count + additionalElements);
                newSlice.CopyFrom(slice);
                slice = newSlice;
            }
        }
        else if (newSize == 0)
        {
            if (slice.Array == this.buffer && 
                slice.Offset + slice.Count == this.position)
            {
                position = slice.Offset; // Reset position to the start of the slice                
            }            
            slice = new ArraySegment<T>();
        }
        else
        {
            int elementsToRemove = slice.Count - newSize;
            if (slice.Array == this.buffer && 
                slice.Offset + slice.Count == this.position)
            {
                position -= elementsToRemove;
                slice = new ArraySegment<T>(buffer, slice.Offset, newSize);
            }
            else
            {
                slice = new ArraySegment<T>(slice.Array, slice.Offset, newSize);
            }
        }
        if (threadSafe) bufferLock.Exit();
    }
}
