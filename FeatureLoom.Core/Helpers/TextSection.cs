using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Helpers
{
    public readonly struct TextSection : IEnumerable<char>
    {
        public static readonly TextSection Empty = new TextSection("");

        readonly string text;
        readonly int startIndex;
        public readonly int length;

        public TextSection(string text) : this()
        {
            this.text = text;
            this.startIndex = 0;
            this.length = text.Length;
        }

        public TextSection(string text, int startIndex) : this()
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

        public TextSection(string text, int startIndex, int length) : this()
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
        public TextSection SubSection(int startIndex) => new TextSection(text, this.startIndex + startIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TextSection SubSection(int startIndex, int length) => new TextSection(text, this.startIndex + startIndex, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(TextSection other, out int index)
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

        public struct SplitEnumerator : IEnumerator<TextSection>
        {
            TextSection original;
            TextSection remaining;
            TextSection current;
            char seperator;
            bool skipEmpty;
            bool finished;

            public SplitEnumerator(TextSection original, char seperator, bool skipEmpty)
            {
                this.original = original;
                this.remaining = original;
                this.current = TextSection.Empty;
                this.seperator = seperator;
                this.skipEmpty = skipEmpty;
                this.finished = false;
            }

            public TextSection Current => current;

            object IEnumerator.Current => current;

            public void Dispose() { }

            public bool MoveNext()
            {
                if (finished) return false;

                while (true)
                {
                    if (remaining.TryFindIndex(seperator, out int index))
                    {
                        current = remaining.SubSection(0, index);
                        remaining = remaining.SubSection(index + 1);
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

        public EnumerableHelper<TextSection, SplitEnumerator> Split(char separator, bool skipEmpty = false)
        {
            return new EnumerableHelper<TextSection, SplitEnumerator>(new SplitEnumerator(this, separator, skipEmpty));
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

        public static implicit operator TextSection(string text) => new TextSection(text);
        public static implicit operator string(TextSection textSection) => textSection.text;
    }
}
