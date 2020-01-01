using System;
using System.Collections;
using System.Collections.Generic;

namespace FeatureFlowFramework.Helper
{
    public class PriorityQueue<T> : IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>, ICollection
    {
        private LinkedList<T> linkedList = new LinkedList<T>();
        private Comparer<T> comparer;

        public PriorityQueue(Comparer<T> comparer)
        {
            this.comparer = comparer;
        }

        public void Clear()
        {
            linkedList.Clear();
        }

        private bool Contains(T item)
        {
            return linkedList.Contains(item);
        }

        private void CopyTo(T[] array, int arrayIndex)
        {
            linkedList.CopyTo(array, arrayIndex);
        }

        public T Dequeue(bool highest = true)
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

        public bool TryDequeue(out T item)
        {
            if (this.Count > 0)
            {
                item = this.Dequeue();
                return true;
            }
            else
            {
                item = default;
                return false;
            }
        }

        public T Peek()
        {
            if (linkedList.Count == 0) throw new InvalidOperationException("The queue is empty!");

            var value = linkedList.First.Value;
            return value;
        }

        public T[] ToArray()
        {
            var array = new T[linkedList.Count];
            CopyTo(array, 0);
            return array;
        }

        public void Enqueue(T item)
        {
            var node = linkedList.First;
            if (node == null) linkedList.AddFirst(new LinkedListNode<T>(item));
            else
            {
                while (node != null && comparer.Compare(item, node.Value) < 0) node = node.Next;
                if (node == null) linkedList.AddLast(new LinkedListNode<T>(item));
                else linkedList.AddBefore(node, new LinkedListNode<T>(item));
            }
        }

        public int Count => ((IReadOnlyCollection<T>)linkedList).Count;

        public bool IsSynchronized => ((ICollection)linkedList).IsSynchronized;

        public object SyncRoot => ((ICollection)linkedList).SyncRoot;

        public void CopyTo(Array array, int index)
        {
            ((ICollection)linkedList).CopyTo(array, index);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)linkedList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)linkedList).GetEnumerator();
        }
    }
}