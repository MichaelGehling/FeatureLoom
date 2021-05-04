using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers.Misc
{
    public class Box<T> where T : struct
    {
        public T value;

        public Box()
        {
        }

        public Box(T value)
        {
            this.value = value;
        }

        public static implicit operator T(Box<T> box) => box.value;
    }
}
