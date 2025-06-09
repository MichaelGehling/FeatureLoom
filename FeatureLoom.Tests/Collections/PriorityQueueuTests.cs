using FeatureLoom.Collections;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Collections;

public class PriorityQueueTests
{
    private class IntDescendingComparer : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x); // Higher value = higher priority
    }

    [Fact]
    public void Enqueue_And_Dequeue_HighestPriority()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(5);
        queue.Enqueue(1);
        queue.Enqueue(3);

        Assert.Equal(3, queue.Count);
        Assert.Equal(5, queue.Dequeue(true)); // 5 is highest with default comparer
        Assert.Equal(2, queue.Count);
        Assert.Equal(3, queue.Dequeue(true));
        Assert.Equal(1, queue.Count);
        Assert.Equal(1, queue.Dequeue(true));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Enqueue_And_Dequeue_HighestPriority_CustomComparer()
    {
        var queue = new PriorityQueue<int>(new IntDescendingComparer());
        queue.Enqueue(5);
        queue.Enqueue(1);
        queue.Enqueue(3);

        Assert.Equal(3, queue.Count);
        Assert.Equal(1, queue.Dequeue(true)); // 1 is highest with descending comparer
        Assert.Equal(3, queue.Dequeue(true));
        Assert.Equal(5, queue.Dequeue(true));
    }

    [Fact]
    public void Dequeue_LowestPriority()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(2);
        queue.Enqueue(4);
        queue.Enqueue(1);

        Assert.Equal(1, queue.Dequeue(false)); // 1 is lowest with default comparer
        Assert.Equal(2, queue.Dequeue(false));
        Assert.Equal(4, queue.Dequeue(false));
    }

    [Fact]
    public void TryDequeue_ReturnsFalseOnEmpty()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        Assert.False(queue.TryDequeue(out var _, true));
        Assert.False(queue.TryDequeue(out var _, false));
    }

    [Fact]
    public void TryDequeue_ReturnsTrueAndRemovesItem()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(10);
        Assert.True(queue.TryDequeue(out var value, true));
        Assert.Equal(10, value);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Peek_ReturnsHighestPriorityWithoutRemoving()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(2);
        queue.Enqueue(1);
        Assert.Equal(2, queue.Peek(true)); // 2 is highest with default comparer
        Assert.Equal(1, queue.Peek(false)); // 1 is lowest
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Peek_ThrowsOnEmpty()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        Assert.Throws<InvalidOperationException>(() => queue.Peek(true));
        Assert.Throws<InvalidOperationException>(() => queue.Peek(false));
    }

    [Fact]
    public void Dequeue_ThrowsOnEmpty()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue(true));
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue(false));
    }

    [Fact]
    public void Contains_ReturnsTrueIfPresent()
    {
        var queue = new PriorityQueue<string>(Comparer<string>.Default);
        queue.Enqueue("a");
        queue.Enqueue("b");
        Assert.True(queue.Contains("a"));
        Assert.False(queue.Contains("c"));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Clear();
        Assert.Equal(0, queue.Count);
        Assert.False(queue.Contains(1));
    }

    [Fact]
    public void ToArray_ReturnsAllItemsInOrder()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(3);
        queue.Enqueue(1);
        queue.Enqueue(2);
        var arr = queue.ToArray();
        Assert.Equal(new[] { 3, 2, 1 }, arr); // Highest to lowest
    }

    [Fact]
    public void CopyTo_CopiesElements()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(2);
        queue.Enqueue(1);
        var arr = new int[2];
        queue.CopyTo(arr, 0);
        Assert.Equal(new[] { 2, 1 }, arr); // Highest to lowest
    }

    [Fact]
    public void IEnumerable_EnumeratesInPriorityOrder()
    {
        var queue = new PriorityQueue<int>(Comparer<int>.Default);
        queue.Enqueue(3);
        queue.Enqueue(1);
        queue.Enqueue(2);

        var list = new List<int>();
        foreach (var item in queue)
        {
            list.Add(item);
        }
        Assert.Equal(new[] { 3, 2, 1 }, list); // Highest to lowest
    }
}