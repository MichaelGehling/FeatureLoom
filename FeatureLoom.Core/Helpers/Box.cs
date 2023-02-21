namespace FeatureLoom.Helpers
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
    }
}