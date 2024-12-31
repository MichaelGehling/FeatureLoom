using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Helpers
{
    public struct TextSegment : IEnumerable<char>
    {
        public static readonly TextSegment Empty = new TextSegment("");

        readonly string text;
        readonly int startIndex;
        public readonly int length;
        int? hashCode;

        public TextSegment(string text) : this()
        {
            this.text = text;
            this.startIndex = 0;
            this.length = text.Length;
        }

        public TextSegment(string text, int startIndex) : this()
        {
            if (startIndex >= text.Length)
            {
                this.text = "";
                return;
            }

            this.text = text;
            this.startIndex = startIndex;
            this.length = text.Length - startIndex;
        }

        public TextSegment(string text, int startIndex, int length) : this()
        {
            if (startIndex + length > text.Length)
            {
                this.text = "";
                return;
            }

            this.text = text;
            this.startIndex = startIndex;
            this.length = length;
        }

        public override string ToString() => length == 0 ? "" : text.Substring(startIndex, length);
        public char this[int index] => text[startIndex + index];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TextSegment SubSegment(int startIndex) => new TextSegment(text, this.startIndex + startIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TextSegment SubSegment(int startIndex, int length) => new TextSegment(text, this.startIndex + startIndex, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(TextSegment other, out int index)
        {
            for (index = 0; index < length; index++)
            {
                if (index + other.length > length) return false;
                bool found = true;
                for (int j = 0; j < other.length; j++)
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
        public bool TryFindIndex(char c, out int index)
        {
            for (index = 0; index < length; index++)
            {
                if (text[startIndex + index] == c) return true;
            }
            return false;
        }

        public struct SplitEnumerator : IEnumerator<TextSegment>
        {
            TextSegment original;
            TextSegment remaining;
            TextSegment current;
            char seperator;
            bool skipEmpty;
            bool finished;

            public SplitEnumerator(TextSegment original, char seperator, bool skipEmpty)
            {
                this.original = original;
                this.remaining = original;
                this.current = TextSegment.Empty;
                this.seperator = seperator;
                this.skipEmpty = skipEmpty;
                this.finished = false;
            }

            public TextSegment Current => current;

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
                        if (current.length == 0 && skipEmpty) continue;
                        return true;
                    }
                    else
                    {
                        current = remaining;
                        remaining = Empty;
                        if (current.length == 0 && skipEmpty) return false;
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

        public EnumerableHelper<TextSegment, SplitEnumerator> Split(char separator, bool skipEmpty = false)
        {
            return new EnumerableHelper<TextSegment, SplitEnumerator>(new SplitEnumerator(this, separator, skipEmpty));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<char> GetEnumerator()
        {
            for (int i = 0; i < length; i++)
            {
                yield return text[startIndex + i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static implicit operator TextSegment(string text) => new TextSegment(text);
        public static implicit operator string(TextSegment textSegment) => textSegment.ToString();
        public static bool operator ==(TextSegment left, TextSegment right)
        {
            if (left.length !=  right.length) return false;
            if (left.GetHashCode() != right.GetHashCode()) return false;

            for (int i = 0;i < left.length;i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }

        public static bool operator !=(TextSegment left, TextSegment right) => !(left == right);

        public override bool Equals(object obj)
        {            
            if (obj is TextSegment textSegment) return this == textSegment;
            if (obj is string str) return this == str;
            return false;
        }


        public override int GetHashCode()
        {
            if (!hashCode.HasValue) hashCode = ComputeHashCode();
            return hashCode.Value;
        }


        private int ComputeHashCode()
        {
            // Initial hash values.
            int hash1 = 5381;
            int hash2 = 5381;

            for (int i = 0; i < length; i++)
            {
                // Processing odd indexed characters with hash1 and even indexed characters with hash2.
                if (i % 2 == 0)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ this[i];
                }
                else
                {
                    hash2 = ((hash2 << 5) + hash2) ^ this[i];
                }
            }

            // Combining the hash values.
            return hash1 + (hash2 * 1566083941);
        }
    }
}
