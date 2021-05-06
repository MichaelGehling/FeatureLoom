using System;
using System.Collections;
using System.Collections.Generic;

namespace FeatureLoom.Helpers
{
    public class GenericComparer<T> : IComparer<T>, IComparer
    {
        private readonly Func<T, T, int> compare;

        public GenericComparer(Func<T, T, int> compare)
        {
            this.compare = compare;
        }

        public int Compare(T x, T y)
        {
            return compare(x, y);
        }

        public int Compare(object x, object y)
        {
            if (x is T xT && y is T yT) return compare(xT, yT);
            else throw new Exception($"Wrong types to compare. Expected: {typeof(T)}, X: {x.GetType()}, Y: {y.GetType()}");
        }
    }
}