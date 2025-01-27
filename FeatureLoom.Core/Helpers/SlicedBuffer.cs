using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;


namespace FeatureLoom.Helpers
{
    public class SlicedBuffer<T>
    {
        T[] buffer;
        int capacity;
        int initCapacity;
        int position;
        int wasteLimit;
        int capacityGrowth;

        public SlicedBuffer(int capacity)
        {
            initCapacity = capacity;
            this.capacity = capacity;
            this.wasteLimit = capacity;
            buffer = new T[capacity];
            this.position = 0;
            this.capacityGrowth = 0;
        }

        public SlicedBuffer(int capacity, int wasteLimit)
        {
            initCapacity = capacity;
            this.capacity = capacity;
            this.wasteLimit = wasteLimit <= capacity ? wasteLimit : capacity;
            buffer = new T[capacity];
            this.position = 0;
            this.capacityGrowth = 0;
        }

        public SlicedBuffer(int capacity, int wasteLimit, int capacityGrowth)
        {
            initCapacity = capacity;
            this.capacity = capacity;
            this.wasteLimit = wasteLimit <= capacity ? wasteLimit : capacity;
            buffer = new T[capacity];
            this.position = 0;
            this.capacityGrowth = capacityGrowth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void RenewBuffer()
        {
            buffer = new T[capacity];
            this.position = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<T> GetSlice(int size)
        {
            int leftCapacity = buffer.Length - position;
            if (size <= leftCapacity)
            {
                var slice = new ArraySegment<T>(buffer, position, size);
                position += size;
                return slice;
            }
            else if (size > wasteLimit)
            {
                return new ArraySegment<T>(new T[size]);
            }
            else
            {
                capacity += capacityGrowth;
                RenewBuffer();
                var slice = new ArraySegment<T>(buffer, position, size);
                position += size;
                return slice;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(bool reuseExistingBuffer, bool resetCapacity = false)
        {
            if (reuseExistingBuffer) position = 0;
            else
            {
                if (resetCapacity) capacity = initCapacity;
                RenewBuffer();
            }
        }

        // Returns a new ArraySegment with the extended size, but filled with the elements from the input ArraySegment
        // If the input ArraySegment is the latest element of the sliceBuffer and capacity is sufficient,
        // the data does not has to be copied, but simply the size of the original ArraySegment is increased.
        // This can be used to grow the latest slice step by step very efficiently, as long as no other slice was created in the meantime.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<T> ExtendSlice(ArraySegment<T> slice, int additionalElements)
        {
            if (additionalElements == 0) return slice;
            int leftCapacity = buffer.Length - position;
            if (slice.Array == this.buffer && 
               slice.Offset + slice.Count == this.position &&
               leftCapacity >= additionalElements)
            {
                position += additionalElements;
                return new ArraySegment<T>(buffer, slice.Offset, slice.Count + additionalElements);
            }
            else
            {
                var newSlice = GetSlice(slice.Count + additionalElements);
                newSlice.CopyFrom(slice);
                return newSlice;
            }
        }
    }
}
