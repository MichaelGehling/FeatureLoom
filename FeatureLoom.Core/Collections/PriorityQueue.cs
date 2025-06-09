using System;
using System.Collections;
using System.Collections.Generic;

namespace FeatureLoom.Collections;

/// <summary>
/// A simple priority queue implementation based on a sorted linked list.
/// 
/// Items are inserted in order according to the provided <see cref="IComparer{T}"/>.
/// The highest-priority item (as determined by the comparer) is always at the front of the queue.
/// Enqueue is O(n), while Dequeue and Peek are O(1).
/// Suitable for small to medium-sized queues or scenarios where insertions are infrequent compared to removals.
/// Not thread-safe.
/// <para>
/// <b>Priority Behavior:</b><br/>
/// By default, with <c>Comparer&lt;int&gt;.Default</c>, higher values are considered higher priority and will be dequeued first.<br/>
/// <b>Example:</b>
/// <code>
/// var queue = new PriorityQueue&lt;int&gt;(Comparer&lt;int&gt;.Default);
/// queue.Enqueue(3);
/// queue.Enqueue(1);
/// queue.Enqueue(5);
/// var first = queue.Dequeue(true); // first == 5 (highest value)
/// var lowest = queue.Dequeue(false); // lowest == 1
/// </code>
/// To reverse the order (lowest value first), use a custom comparer or dequeue with <c>queue.Dequeue(false)</c>.
/// </para>
/// </summary>
public sealed class PriorityQueue<T> : IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>, ICollection
{
    private LinkedList<T> linkedList = new LinkedList<T>();
    private IComparer<T> comparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriorityQueue{T}"/> class with the specified comparer.
    /// </summary>
    /// <param name="comparer">The comparer used to determine item priority.</param>
    public PriorityQueue(IComparer<T> comparer)
    {
        this.comparer = comparer;
    }

    /// <summary>
    /// Removes all items from the queue.
    /// </summary>
    public void Clear()
    {
        linkedList.Clear();
    }

    /// <summary>
    /// Determines whether the queue contains a specific item.
    /// </summary>
    public bool Contains(T item)
    {
        return linkedList.Contains(item);
    }

    /// <summary>
    /// Copies the elements of the queue to an array, starting at a particular array index.
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex)
    {
        linkedList.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Removes and returns the item with the highest or lowest priority.
    /// </summary>
    /// <param name="highest">If true, removes the highest-priority item; otherwise, removes the lowest.</param>
    /// <returns>The removed item.</returns>
    /// <exception cref="InvalidOperationException">If the queue is empty.</exception>
    public T Dequeue(bool highest)
    {
        if (linkedList.Count == 0) throw new InvalidOperationException("The queue is empty!");
        T value;
        if (highest)
        {
            value = linkedList.First.Value;
            linkedList.RemoveFirst();
        }
        else
        {
            value = linkedList.Last.Value;
            linkedList.RemoveLast();
        }
        return value;
    }

    /// <summary>
    /// Attempts to remove and return the item with the highest or lowest priority.
    /// </summary>
    /// <param name="item">The removed item, if successful.</param>
    /// <param name="highest">If true, removes the highest-priority item; otherwise, removes the lowest.</param>
    /// <returns>True if an item was dequeued; otherwise, false.</returns>
    public bool TryDequeue(out T item, bool highest)
    {
        if (this.Count > 0)
        {
            item = this.Dequeue(highest);
            return true;
        }
        else
        {
            item = default;
            return false;
        }
    }

    /// <summary>
    /// Returns the item with the highest or lowest priority without removing it.
    /// </summary>
    /// <param name="highest">If true, returns the highest-priority item; otherwise, returns the lowest.</param>
    /// <returns>The item at the front or back of the queue.</returns>
    /// <exception cref="InvalidOperationException">If the queue is empty.</exception>
    public T Peek(bool highest)
    {
        if (linkedList.Count == 0) throw new InvalidOperationException("The queue is empty!");
        return highest ? linkedList.First.Value : linkedList.Last.Value;
    }

    /// <summary>
    /// Returns an array containing all items in the queue.
    /// </summary>
    public T[] ToArray()
    {
        var array = new T[linkedList.Count];
        CopyTo(array, 0);
        return array;
    }

    /// <summary>
    /// Inserts an item into the queue according to its priority.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    public void Enqueue(T item)
    {
        if (linkedList.Count == 0)
        {
            linkedList.AddFirst(item);
            return;
        }

        // Check if item should be new head (highest priority)
        if (comparer.Compare(item, linkedList.First.Value) > 0)
        {
            linkedList.AddFirst(item);
            return;
        }

        // Check if item should be new tail (lowest priority)
        if (comparer.Compare(item, linkedList.Last.Value) <= 0)
        {
            linkedList.AddLast(item);
            return;
        }

        // Otherwise, find the correct position
        var node = linkedList.First;
        while (node != null && comparer.Compare(item, node.Value) <= 0)
        {
            node = node.Next;
        }
        if (node == null)
        {
            linkedList.AddLast(item);
        }
        else
        {
            linkedList.AddBefore(node, item);
        }
    }

    /// <summary>
    /// Gets the number of items in the queue.
    /// </summary>
    public int Count => ((IReadOnlyCollection<T>)linkedList).Count;

    /// <summary>
    /// Gets a value indicating whether access to the queue is synchronized (thread-safe).
    /// </summary>
    public bool IsSynchronized => ((ICollection)linkedList).IsSynchronized;

    /// <summary>
    /// Gets an object that can be used to synchronize access to the queue.
    /// </summary>
    public object SyncRoot => ((ICollection)linkedList).SyncRoot;

    /// <summary>
    /// Copies the elements of the queue to an array, starting at a particular array index.
    /// </summary>
    public void CopyTo(Array array, int index)
    {
        ((ICollection)linkedList).CopyTo(array, index);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the queue.
    /// </summary>
    public LinkedList<T>.Enumerator GetEnumerator()
    {
        return linkedList.GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)linkedList).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<T>)linkedList).GetEnumerator();
    }
}