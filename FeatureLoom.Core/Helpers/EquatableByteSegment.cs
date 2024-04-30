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

        // Implicit conversion from ArraySegment<byte> to ByteSegmentWrapper
        public static implicit operator EquatableByteSegment(ArraySegment<byte> segment)
        {
            return new EquatableByteSegment(segment);
        }

        // Implicit conversion from ByteSegmentWrapper to ArraySegment<byte>
        public static implicit operator ArraySegment<byte>(EquatableByteSegment wrapper)
        {
            return wrapper.segment;
        }

        // Implicit conversion from ByteSegmentWrapper to ArraySegment<byte>
        public static implicit operator byte[](EquatableByteSegment wrapper)
        {
            return wrapper.segment.ToArray();
        }

        public ArraySegment<byte> Segment => segment;
        public byte[] ToArray() => segment.ToArray();

        public bool Equals(EquatableByteSegment other)
        {
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
                for (int i = segment.Offset; i < segment.Offset + segment.Count; i++)
                    hash = hash * 23 + segment.Array[i];
                return hash;
            }
        }
    }

}