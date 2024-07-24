using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        void RenewBuffer()
        {
            buffer = new T[buffer.Length];
            this.position = 0;
        }

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

        public void Reset(bool reuseExistingBuffer, bool resetCapacity = false)
        {
            if (resetCapacity) capacity = initCapacity;

            if (reuseExistingBuffer) position = 0;
            else RenewBuffer();
        }        
    }
}
