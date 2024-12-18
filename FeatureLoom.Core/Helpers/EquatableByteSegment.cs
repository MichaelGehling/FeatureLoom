﻿using System;
using System.Collections;
using FeatureLoom.Extensions;

namespace FeatureLoom.Helpers
{
    public struct EquatableByteSegment : IEquatable<EquatableByteSegment>
    {
        private readonly ArraySegment<byte> segment;
        private int? hashCode;        

        public EquatableByteSegment(ArraySegment<byte> segment)
        {
            this.segment = segment;
        }

        public EquatableByteSegment(byte[] array, int offset, int count)
        {
            this.segment = new ArraySegment<byte>(array, offset, count);
        }

        public EquatableByteSegment(byte[] array)
        {
            this.segment = new ArraySegment<byte>(array);
        }

        public EquatableByteSegment(string str)
        {
            this.segment = new ArraySegment<byte>(str.ToByteArray());
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

        public bool IsValid => segment.Array != null;
        public int Count => segment.Count;
        public bool IsEmptyOrInvalid => !IsValid || segment.Count == 0;

        public ArraySegment<byte> Segment => segment;
        public byte[] ToArray() => segment.ToArray();

        public bool Equals(EquatableByteSegment other)
        {
            if (segment.Count != other.segment.Count) return false;
            if (GetHashCode() != other.GetHashCode()) return false;            

            for (int i = 0; i < segment.Count; i++)
            {
                if (segment.Array[segment.Offset + i] != other.segment.Array[other.segment.Offset + i])
                    return false;
            }

            return true;
        }

        public bool Equals(ArraySegment<byte> other)
        {
            if (segment.Count != other.Count) return false;

            for (int i = 0; i < segment.Count; i++)
            {
                if (segment.Array[segment.Offset + i] != other.Array[other.Offset + i])
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
            if (!hashCode.HasValue) hashCode = ComputeHashCode(Segment);
            return hashCode.Value;
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

        public override string ToString()
        {
            if (segment.Array == null) return null;
            try
            {
                return System.Text.Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
            }
            catch
            {
                return Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
            }
        }
    }

}