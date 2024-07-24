using System;
using FeatureLoom.Extensions;

namespace FeatureLoom.Helpers
{
    public struct EquatableByteSegment : IEquatable<EquatableByteSegment>
    {
        private readonly ArraySegment<byte> segment;
        private readonly int hashCode;

        public EquatableByteSegment(ArraySegment<byte> segment)
        {
            this.segment = segment;
            this.hashCode = ComputeHashCode(segment);
        }

        // Implicit conversion from ArraySegment<byte> to EquatableByteSegment
        public static implicit operator EquatableByteSegment(ArraySegment<byte> segment)
        {
            return new EquatableByteSegment(segment);
        }

        // Implicit conversion from byte[] to EquatableByteSegment
        public static implicit operator EquatableByteSegment(byte[] byteArray)
        {
            return new EquatableByteSegment(new ArraySegment<byte>(byteArray));
        }

        // Implicit conversion from EquatableByteSegment to ArraySegment<byte>
        public static implicit operator ArraySegment<byte>(EquatableByteSegment wrapper)
        {
            return wrapper.segment;
        }

        // Implicit conversion from EquatableByteSegment to ArraySegment<byte>
        public static implicit operator byte[](EquatableByteSegment wrapper)
        {
            return wrapper.segment.ToArray();
        }

        public ArraySegment<byte> Segment => segment;
        public byte[] ToArray() => segment.ToArray();

        public bool Equals(EquatableByteSegment other)
        {
            if (hashCode != other.hashCode) return false;
            if (segment.Count != other.segment.Count) return false;

            for (int i = 0; i < segment.Count; i++)
            {
                if (segment.Array[segment.Offset + i] != other.segment.Array[other.segment.Offset + i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is EquatableByteSegment other && Equals(other);
        }

        public override int GetHashCode()
        {
            return hashCode;
        }

        private static int ComputeHashCode(ArraySegment<byte> segment)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                var limit = segment.Offset + segment.Count;
                for (int i = segment.Offset; i < limit; i++)
                    hash = hash * 23 + segment.Array[i];
                return hash;
            }
        }
    }

}