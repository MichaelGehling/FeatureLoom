using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Helpers
{
    public class PatternExtractor
    {
        List<string> staticParts = new List<string>();
        int size = 0;
        string pattern;

        public int Size => size;
        public string Pattern => pattern;

        public PatternExtractor(string pattern)
        {
            this.pattern = pattern;
            int pos = 0;                
            while(pattern.TryExtract(pos, null, "{", out string staticPart, out pos))
            {
                staticParts.Add(staticPart);
                size++;
                while (pattern.Length > pos && pattern[pos++] != '}') ;
            }
            if (pos == pattern.Length) staticParts.Add("");
            else staticParts.Add(pattern.Substring(pos));
        }

        public bool TryExtract<T1>(string sourceString, out T1 item1)
            where T1 : IConvertible
        {
            item1 = default;
            int pos = 0;
            if (staticParts.Count <= 1) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            return true;
        }

        public bool TryExtract<T1,T2>(string sourceString, out T1 item1, out T2 item2)
            where T1 : IConvertible
            where T2 : IConvertible
        {
            item1 = default;
            item2 = default;
            int pos = 0;
            if (staticParts.Count <= 2) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
            return true;
        }

        public bool TryExtract<T1, T2, T3>(string sourceString, out T1 item1, out T2 item2, out T3 item3)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
        {
            item1 = default;
            item2 = default;
            item3 = default;
            int pos = 0;
            if (staticParts.Count <= 3) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
            return true;
        }

        public bool TryExtract<T1, T2, T3, T4>(string sourceString, out T1 item1, out T2 item2, out T3 item3, out T4 item4)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
        {
            item1 = default;
            item2 = default;
            item3 = default;
            item4 = default;
            int pos = 0;
            if (staticParts.Count <= 4) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
            return true;
        }

        public bool TryExtract<T1, T2, T3, T4, T5>(string sourceString, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
        {
            item1 = default;
            item2 = default;
            item3 = default;
            item4 = default;
            item5 = default;
            int pos = 0;
            if (staticParts.Count <= 5) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
            return true;
        }

        public bool TryExtract<T1, T2, T3, T4, T5, T6>(string sourceString, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
            where T6 : IConvertible
        {
            item1 = default;
            item2 = default;
            item3 = default;
            item4 = default;
            item5 = default;
            item6 = default;
            int pos = 0;
            if (staticParts.Count <= 6) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[5], staticParts[6], out item6, out pos)) return false;
            return true;
        }

        public bool TryExtract<T1, T2, T3, T4, T5, T6, T7>(string sourceString, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
            where T6 : IConvertible
            where T7 : IConvertible
        {
            item1 = default;
            item2 = default;
            item3 = default;
            item4 = default;
            item5 = default;
            item6 = default;
            item7 = default;
            int pos = 0;
            if (staticParts.Count <= 7) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[5], staticParts[6], out item6, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[6], staticParts[7], out item7, out pos)) return false;
            return true;
        }

        public bool TryExtract<T1, T2, T3, T4, T5, T6, T7, T8>(string sourceString, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
            where T6 : IConvertible
            where T7 : IConvertible
            where T8 : IConvertible
        {
            item1 = default;
            item2 = default;
            item3 = default;
            item4 = default;
            item5 = default;
            item6 = default;
            item7 = default;
            item8 = default;
            int pos = 0;
            if (staticParts.Count <= 8) return false;
            if (!sourceString.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[5], staticParts[6], out item6, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[6], staticParts[7], out item7, out pos)) return false;
            if (!sourceString.TryExtract(pos, staticParts[7], staticParts[8], out item8, out pos)) return false;
            return true;
        }


    }

}
