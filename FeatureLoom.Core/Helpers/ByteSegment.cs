using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;

namespace FeatureLoom.Helpers
{
    public struct ByteSegment : IEquatable<ByteSegment>, IEnumerable<byte>
    {

        public static readonly ByteSegment Empty = new ByteSegment(Array.Empty<byte>());

        private readonly ArraySegment<byte> segment;
        private int? hashCode;        

        public ByteSegment(ArraySegment<byte> segment)
        {
            this.segment = segment;
        }

        public ByteSegment(byte[] array, int offset, int count)
        {
            this.segment = new ArraySegment<byte>(array, offset, count);
        }

        public ByteSegment(byte[] array)
        {
            this.segment = new ArraySegment<byte>(array);
        }

        public ByteSegment(string str)
        {
            this.segment = new ArraySegment<byte>(str.ToByteArray());
        }

        // Implicit conversion from ArraySegment<byte> to EquatableByteSegment
        public static implicit operator ByteSegment(ArraySegment<byte> segment)
        {
            return new ByteSegment(segment);
        }

        // Implicit conversion from byte[] to EquatableByteSegment
        public static implicit operator ByteSegment(byte[] byteArray)
        {
            return new ByteSegment(new ArraySegment<byte>(byteArray));
        }

        // Implicit conversion from EquatableByteSegment to ArraySegment<byte>
        public static implicit operator ArraySegment<byte>(ByteSegment wrapper)
        {
            return wrapper.segment;
        }

        // Implicit conversion from EquatableByteSegment to ArraySegment<byte>
        public static implicit operator byte[](ByteSegment wrapper)
        {
            return wrapper.segment.ToArray();
        }

        public byte this[int index] => segment.Get(index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment SubSegment(int startIndex) => new ByteSegment(segment.Array, startIndex + segment.Offset, segment.Count - startIndex);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment SubSegment(int startIndex, int length) => new ByteSegment(segment.Array, startIndex + segment.Offset, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(ByteSegment other, out int index)
        {
            for (index = 0; index < segment.Count; index++)
            {
                if (index + other.segment.Count > segment.Count) return false;
                bool found = true;
                for (int j = 0; j < other.segment.Count; j++)
                {
                    if (this[index + j] != other[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(byte b, out int index)
        {
            for (index = 0; index < segment.Count; index++)
            {
                if (this[index] == b) return true;
            }
            return false;
        }


        public bool IsValid => segment.Array != null;
        public int Count => segment.Count;
        public bool IsEmptyOrInvalid => !IsValid || segment.Count == 0;

        public ArraySegment<byte> AsArraySegment => segment;
        public byte[] ToArray() => segment.ToArray();

        public struct SplitEnumerator : IEnumerator<ByteSegment>
        {
            ByteSegment original;
            ByteSegment remaining;
            ByteSegment current;
            byte seperator;
            bool skipEmpty;
            bool finished;

            public SplitEnumerator(ByteSegment original, byte seperator, bool skipEmpty)
            {
                this.original = original;
                this.remaining = original;
                this.current = Empty;
                this.seperator = seperator;
                this.skipEmpty = skipEmpty;
                this.finished = false;
            }

            public ByteSegment Current => current;

            object IEnumerator.Current => current;

            public void Dispose() { }

            public bool MoveNext()
            {
                if (finished) return false;

                while (true)
                {
                    if (remaining.TryFindIndex(seperator, out int index))
                    {
                        current = remaining.SubSegment(0, index);
                        remaining = remaining.SubSegment(index + 1);
                        if (current.Count == 0 && skipEmpty) continue;
                        return true;
                    }
                    else
                    {
                        current = remaining;
                        remaining = Empty;
                        if (current.Count == 0 && skipEmpty) return false;
                        finished = true;
                        return true;
                    }
                }
            }

            public void Reset()
            {
                remaining = original;
                finished = false;
            }
        }

        public EnumerableHelper<ByteSegment, SplitEnumerator> Split(byte separator, bool skipEmpty = false)
        {
            return new EnumerableHelper<ByteSegment, SplitEnumerator>(new SplitEnumerator(this, separator, skipEmpty));
        }

        public bool Equals(ByteSegment other)
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

        public override bool Equals(object obj)
        {
            return obj is ByteSegment other && Equals(other);
        }

        public static bool operator ==(ByteSegment left, ByteSegment right) => left.Equals(right);        

        public static bool operator !=(ByteSegment left, ByteSegment right) => !left.Equals(right);

        public override int GetHashCode()
        {
            if (!hashCode.HasValue) hashCode = ComputeHashCode(AsArraySegment);
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

        public IEnumerator<byte> GetEnumerator()
        {
            return ((IEnumerable<byte>)segment).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)segment).GetEnumerator();
        }
    }

}