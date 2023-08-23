using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers
{
    public readonly struct EnumerableHelper<T, E> : IEnumerable<T> where E : IEnumerator<T>
    {
        readonly E enumerator;

        public EnumerableHelper(E enumerator)
        {
            this.enumerator = enumerator;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return enumerator;
        }
    }
}
