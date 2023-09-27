using System;
using System.Data;

namespace FeatureLoom.Helpers
{

    public interface IBox
    {
        void Clear();

        T GetValue<T>();
        void SetValue<T>(T value);
    }

    public class Box<T> : IBox
    {
        public T value;

        public Box()
        {
        }

        public Box(T value)
        {
            this.value = value;
        }        

        public void Clear() => value = default;
        
        public static implicit operator Box<T>(T value) => new Box<T>(value);

        public override bool Equals(object obj)
        {
            return value.Equals(obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public static bool operator ==(Box<T> box, T other) => box.value.Equals(other);
        public static bool operator ==(T other, Box<T> box) => box.value.Equals(other);

        public static bool operator !=(Box<T> box, T other) => !box.value.Equals(other);
        public static bool operator !=(T other, Box<T> box) => !box.value.Equals(other);

        public override string ToString()
        {
            return value.ToString();
        }

        public T1 GetValue<T1>()
        {
            if (value is T1 castedValue) return castedValue;
            throw new Exception($"Wrong type! Box has a value of type {typeof(T)}, while requested was a {typeof(T1)}");
        }

        public void SetValue<T1>(T1 value)
        {
            if (value is T castedValue) this.value = castedValue;
            throw new Exception($"Wrong type! Box has a value of type {typeof(T)}, while set was a {typeof(T1)}");
        }
    }
}