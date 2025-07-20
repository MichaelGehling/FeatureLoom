using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Extensions
{
    public static class WildcardMatchExtension
    {
        public static bool MatchesWildcard(this string text, string pattern)
        {
            if (text == null) return false;
            if (pattern == null) return false;
            if (pattern.Length == 0) return text.Length == 0 ? true : false;            

            VariableTextSection remainingText = new VariableTextSection(text);
            VariableTextSection remainingPattern = new VariableTextSection(pattern);

            while(remainingPattern.length > 0)
            {
                VariableTextSection sectionWithoutWildcard = remainingPattern.SectionUntilNextWildcard();
                if (sectionWithoutWildcard.length > remainingText.length) return false;
                for(int i = 0; i < sectionWithoutWildcard.length; i++)
                {
                    if (remainingText[i] != sectionWithoutWildcard[i]) return false;
                }

                remainingPattern.SkipChars(sectionWithoutWildcard.length);
                remainingText.SkipChars(sectionWithoutWildcard.length);
                if (remainingPattern.length == 0) return true;

                char wildCard = remainingPattern[0];
                remainingPattern.SkipChars(1);
                if (wildCard == '?')
                {
                    if (remainingText.length == 0) return false;
                    remainingText.SkipChars(1);
                }
                else if (wildCard == '*')
                {
                    sectionWithoutWildcard = remainingPattern.SectionUntilNextWildcard();
                    int minRandomCharCount = 0;                    
                    while (sectionWithoutWildcard.length == 0 && remainingPattern.length > 0)
                    {
                        wildCard = remainingPattern[0];
                        remainingPattern.SkipChars(1);
                        if (wildCard == '?') minRandomCharCount++;
                        sectionWithoutWildcard = remainingPattern.SectionUntilNextWildcard();
                    }
                    if (sectionWithoutWildcard.length == 0)
                    {
                        if (remainingText.length < minRandomCharCount) return false;
                        else return true;
                    }
                    else
                    {
                        if (!remainingText.TryFindIndex(sectionWithoutWildcard, out int index)) return false;
                        if (index < minRandomCharCount) return false;
                        remainingText.SkipChars(index + sectionWithoutWildcard.length);
                        remainingPattern.SkipChars(sectionWithoutWildcard.length);
                    }
                }
            }
            return true;
        }

        private struct VariableTextSection
        {
            public static readonly VariableTextSection Empty = new VariableTextSection("");

            string text;
            int startIndex;
            public int length;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SkipChars(int numChars)
            {
                startIndex += numChars;
                length -= numChars;
                if (length < 0)
                {
                    text = "";
                    startIndex = 0;
                    length = 0;
                }
            }

            public VariableTextSection(string text) : this()
            {
                this.text = text;
                this.startIndex = 0;
                this.length = text.Length;
            }

            public VariableTextSection(string text, int startIndex) : this()
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

            public VariableTextSection(string text, int startIndex, int length) : this()
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
            public VariableTextSection SubSection(int startIndex) => new VariableTextSection(text, this.startIndex + startIndex);
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public VariableTextSection SubSection(int startIndex, int length) => new VariableTextSection(text, this.startIndex + startIndex, length);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryFindIndex(VariableTextSection other, out int index)
            {                                
                for (index = 0; index < length; index++)
                {
                    if (index + other.length > length) return false;
                    bool found = true;
                    for(int j = 0; j < other.length; j++)
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VariableTextSection SectionUntilNextWildcard(this VariableTextSection textSection)
        {
            for(int i = 0; i < textSection.length; i++)
            {
                char c = textSection[i];
                if (c == '?' || c == '*') return textSection.SubSection(0, i);
            }
            return textSection;
        }

        public static bool MatchesWildcard(this string text, string wildcardString, bool ignoreCase)
        {
            if (ignoreCase == true)
            {
                return text.ToLower().MatchesWildcard(wildcardString.ToLower());
            }
            else
            {
                return text.MatchesWildcard(wildcardString);
            }
        }
    }
}