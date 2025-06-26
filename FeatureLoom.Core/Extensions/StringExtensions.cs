using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FeatureLoom.Extensions
{
    public static partial class StringExtensions
    {
        public static bool StartsWith(this string str, char c)
        {
            return !str.EmptyOrNull() && str[0] == c;
        }

        public static bool Contains(this string str, char c)
        {
            if (str == null) throw new NullReferenceException();

            foreach (var sc in str)
            {
                if (sc == c) return true;
            }
            return false;
        }

        public static byte[] ToByteArray(this string str, Encoding encoding = default)
        {
            if (encoding == default) encoding = Encoding.UTF8;
            return encoding.GetBytes(str);
        }


        public static string AddToPath(this string pathBase, string pathExtension, char seperator = '\\')
        {
            string temp = pathBase;
            if ((pathBase.Length > 0 && pathBase.Last() != seperator) &&
                (pathExtension.Length == 0 || pathExtension.First() != seperator)) temp += seperator;
            temp += pathExtension;
            return temp;
        }
        
        public static string RemoveLastPathElement(this string path, char seperator = '\\')
        {
            bool didEndWithSeperator = path.EndsWith(seperator.ToString());            
            if (didEndWithSeperator)
            {
                path = path.TrimCharEnd(seperator);
            }

            var index = path.LastIndexOf(seperator);
            if (index == -1)
            {
                if (didEndWithSeperator) return seperator.ToString();
                else return "";
            }
            else
            {
                path = path.Substring(0, index + 1);

                bool endsWithSeperator = path.EndsWith(seperator.ToString());
                if (didEndWithSeperator && !endsWithSeperator) path += seperator;
                else if (!didEndWithSeperator && endsWithSeperator) path = path.TrimCharEnd(seperator);

                return path;
            }            
        }

        public static string GetLastPathElement(this string path, char seperator = '\\')
        {
            path = path.TrimCharEnd(seperator);

            var index = path.LastIndexOf(seperator);
            return path.Substring(index + 1);
        }

        public static string MakeValidFilename(this string fileName, char replacementChar = '_')
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, replacementChar);
            }
            return fileName;
        }

        public static string MakeValidFilePath(this string filePath, char replacementChar = '_')
        {            
            foreach (char c in Path.GetInvalidPathChars())
            {
                filePath = filePath.Replace(c, replacementChar);
            }
            return filePath;
        }

        public static string TextWrap(this string input, int maxCharsPerLine, string nextLine)
        {
            if (input == null) return null;            
            bool whiteSpaceFound = false;
            List<int> potentialBreaks = new List<int>();
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsWhiteSpace(input[i])) whiteSpaceFound = true;
                else if (whiteSpaceFound)
                {
                    potentialBreaks.Add(i);
                    whiteSpaceFound = false;
                }
            }

            StringBuilder sb = new StringBuilder();
            int lastBreak = 0;
            for (int i = 0; i < potentialBreaks.Count; i++)
            {
                if (i + 1 == potentialBreaks.Count)
                {
                    if (input.Length - lastBreak > maxCharsPerLine)
                    {
#if NETSTANDARD2_1_OR_GREATER  
                        sb.Append(input.AsSpan(lastBreak, potentialBreaks[i] - lastBreak));
#else
                        sb.Append(input.Substring(lastBreak, potentialBreaks[i] - lastBreak));
#endif                        
                        sb.Append(nextLine);
                        lastBreak = potentialBreaks[i];
                    }
                }
                else
                {
                    if (potentialBreaks[i + 1] - lastBreak > maxCharsPerLine)
                    {
#if NETSTANDARD2_1_OR_GREATER  
                        sb.Append(input.AsSpan(lastBreak, potentialBreaks[i] - lastBreak));
#else
                        sb.Append(input.Substring(lastBreak, potentialBreaks[i] - lastBreak));
#endif
                        sb.Append(nextLine);
                        lastBreak = potentialBreaks[i];
                    }
                }
            }
#if NETSTANDARD2_1_OR_GREATER
            sb.Append(input.AsSpan(lastBreak));
#else
            sb.Append(input.Substring(lastBreak));
#endif
            return sb.ToString();
        }

        public static string TrimChar(this string str, char trimChar)
        {
            TextSegment segment = str;
            return segment.Trim(trimChar).ToString();
        }

        public static string TrimCharStart(this string str, char trimChar)
        {
            TextSegment segment = str;
            return segment.TrimStart(trimChar).ToString();
        }

        public static string TrimCharEnd(this string str, char trimChar)
        {
            TextSegment segment = str;
            return segment.TrimEnd(trimChar).ToString();
        }

        public static string TrimEnd(this string str, string trimStr)
        {
            TextSegment segment = str;
            return segment.TrimEnd(trimStr).ToString();
        }

        public static string TrimStart(this string str, string trimStr)
        {
            TextSegment segment = str;
            return segment.TrimStart(trimStr).ToString();
        }

        private static InMemoryCache<string, PatternExtractor> extractionPatternCache = new InMemoryCache<string, PatternExtractor>(p => 50 + p.Size*25, 
            new InMemoryCache<string, PatternExtractor>.CacheSettings() 
            { 
                targetCacheSizeInByte = 80_000, 
                cacheSizeMarginInByte = 10_000, 
                maxUnusedTimeInSeconds = 60 * 60 * 24 * 30,
                cleanUpPeriodeInSeconds = 60 * 60
            });

        public static bool TryExtract<T1>(this string str, string pattern, out T1 item1) 
            where T1 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }
            return extractor.TryExtract(str, out item1);
        }

        public static bool TryExtract<T1, T2>(this string str, string pattern, out T1 item1, out T2 item2) 
            where T1 : IConvertible 
            where T2 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }
            return extractor.TryExtract(str, out item1, out item2);
        }

        public static bool TryExtract<T1, T2, T3>(this string str, string pattern, out T1 item1, out T2 item2, out T3 item3) 
            where T1 : IConvertible 
            where T2 : IConvertible
            where T3 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }
            return extractor.TryExtract(str, out item1, out item2, out item3);
        }

        public static bool TryExtract<T1, T2, T3, T4>(this string str, string pattern, out T1 item1, out T2 item2, out T3 item3, out T4 item4)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }
            return extractor.TryExtract(str, out item1, out item2, out item3, out item4);
        }

        public static bool TryExtract<T1, T2, T3, T4, T5>(this string str, string pattern, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }
            return extractor.TryExtract(str, out item1, out item2, out item3, out item4, out item5);
        }

        public static bool TryExtract<T1, T2, T3, T4, T5, T6>(this string str, string pattern, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
            where T6 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }
            return extractor.TryExtract(str, out item1, out item2, out item3, out item4, out item5, out item6);
        }

        public static bool TryExtract<T1, T2, T3, T4, T5, T6, T7>(this string str, string pattern, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
            where T6 : IConvertible
            where T7 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }
            return extractor.TryExtract(str, out item1, out item2, out item3, out item4, out item5, out item6, out item7);
        }

        public static bool TryExtract<T1, T2, T3, T4, T5, T6, T7, T8>(this string str, string pattern, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8)
            where T1 : IConvertible
            where T2 : IConvertible
            where T3 : IConvertible
            where T4 : IConvertible
            where T5 : IConvertible
            where T6 : IConvertible
            where T7 : IConvertible
            where T8 : IConvertible
        {
            if (!extractionPatternCache.TryGet(pattern, out var extractor))
            {
                extractor = new PatternExtractor(pattern);
                extractionPatternCache.Add(pattern, extractor);
            }            
            return extractor.TryExtract(str, out item1, out item2, out item3, out item4, out item5, out item6, out item7, out item8);
        }

        public static bool TryExtract<T>(this string str, string startExtractAfter, string endExtractBefore, out T extract) where T : IConvertible
        {
            return str.TryExtract(0, startExtractAfter, endExtractBefore, out extract, out _);
        }

        public static bool TryExtract<T>(this string str, int startIndex, string startExtractAfter, string endExtractBefore, out T extract) where T : IConvertible
        {
            return str.TryExtract(startIndex, startExtractAfter, endExtractBefore, out extract, out _);
        }

        public static bool TryExtract<T>(this string str, string startExtractAfter, string endExtractBefore, out T extract, out int restStartIndex) where T : IConvertible
        {
            return str.TryExtract(0, startExtractAfter, endExtractBefore, out extract, out restStartIndex);
        }

        /// <summary>
        /// Extracts a part of a string and converts it to the specified (convertible) type.
        /// Note: Uses the current threads culture for conversions
        /// </summary>
        /// <typeparam name="T">The convertible type the extracted part is converted to</typeparam>
        /// <param name="str">The input string</param>
        /// <param name="startIndex">The index where the search for the startmarker starts</param>
        /// <param name="startExtractAfter"></param>
        /// <param name="endExtractBefore"></param>
        /// <param name="extract"></param>
        /// <param name="restStartIndex"></param>
        /// <returns></returns>
        public static bool TryExtract<T>(this string str, int startIndex, string startExtractAfter, string endExtractBefore, out T extract, out int restStartIndex, bool includeSearchStrings = false) where T : IConvertible
        {
            TextSegment segment = str;
            return segment.TryExtract(startIndex, startExtractAfter ?? TextSegment.Empty, endExtractBefore ?? TextSegment.Empty, out extract, out restStartIndex, includeSearchStrings);
        }

        public static string Substring(this string str, string startAfter, string endBefore = null)
        {
            return str.Substring(0, startAfter ?? TextSegment.Empty, endBefore ?? TextSegment.Empty, out _);
        }

        public static string Substring(this string str, int startIndex, string startAfter, string endBefore, out int restStartIndex, bool includeSearchStrings = false)
        {
            TextSegment segment = str;
            return segment.SubSegment(startIndex, startAfter ?? TextSegment.Empty, endBefore ?? TextSegment.Empty, out restStartIndex, includeSearchStrings);            
        }

        public static string ReplaceBetween(this string str, string startAfter, string endBefore, string replacement, bool removeAlsoSearchStrings = false)
        {
            int startPos = 0;
            int endPos = str.Length;
            if (!startAfter.EmptyOrNull())
            {
                startPos = str.IndexOf(startAfter);
            }
            if (startPos == -1) return str;
            if (!removeAlsoSearchStrings) startPos += startAfter.Length;
            if (!endBefore.EmptyOrNull())
            {
                endPos = str.IndexOf(endBefore, startPos);
            }
            if (endPos == -1) return str;
            if (removeAlsoSearchStrings) endPos += endBefore.Length;

            return str.Substring(0, startPos) + replacement + str.Substring(endPos);
        }

        public static string ReplaceAllBetween(this string str, string startAfter, string endBefore, string replacement, bool removeAlsoSearchStrings = false)
        {
            if (startAfter.EmptyOrNull() || endBefore.EmptyOrNull()) return ReplaceBetween(str, startAfter, endBefore, replacement, removeAlsoSearchStrings);
            int startPos = 0;
            bool done = false;
            while(true)
            {                
                startPos = str.IndexOf(startAfter, startPos);
                if (startPos == -1) return str;
                if (!removeAlsoSearchStrings) startPos += startAfter.Length;
                int endPos = str.IndexOf(endBefore, startPos);
                if (endPos == -1) return str;
                if (removeAlsoSearchStrings) endPos += endBefore.Length;
                str = str.Substring(0, startPos) + replacement + str.Substring(endPos);                
                startPos = removeAlsoSearchStrings ? 
                    startPos + replacement.Length: 
                    startPos + replacement.Length + endBefore.Length;
                if (startPos >= str.Length) return str;
            }
        }

        public static string InsertBefore(this string str, string marker, string insertion, bool backwardSearch = false)
        {
            int startPos = 0;
            if (!marker.EmptyOrNull())
            {
                if (backwardSearch) startPos = str.LastIndexOf(marker);
                else startPos = str.IndexOf(marker);
            }
            return str.Insert(startPos, insertion);
        }

        public static string InsertAfter(this string str, string marker, string insertion, bool backwardSearch = false)
        {
            int startPos = 0;
            if (!marker.EmptyOrNull())
            {
                if (backwardSearch) startPos = str.LastIndexOf(marker) + marker.Length;
                else startPos = str.IndexOf(marker) + marker.Length;
            }
            return str.Insert(startPos, insertion);
        }

        public static bool ContainsBefore(this string str, string searchStr, string endBefore)
        {
            int endPos = 0;
            if (!endBefore.EmptyOrNull())
            {
                endPos = str.IndexOf(endBefore);
            }

            return str.IndexOf(searchStr, 0, endPos) >= 0;
        }

        /// <summary>
        /// Conveerts a string to an convertible type.
        /// Note: Uses the current threads culture for conversions if no provider is specified.
        /// </summary>
        public static T ConvertTo<T>(this string str, IFormatProvider provider = null) where T : IConvertible
        {            
            TextSegment segment = str;
            if (segment.TryToType<T>(out T result, provider)) return result;
            throw new InvalidCastException($"Cannot convert string '{str}' to type {typeof(T).Name}.");
        }

        public static string GetStringAndClear(this StringBuilder sb)
        {
            string str = sb.ToString();
            sb.Clear();
            return str;
        }

#if NETSTANDARD2_0
        public static string[] Split(this string str, char seperator, StringSplitOptions options)
        {
            return str.Split(seperator.ToSingleEntryArray(), options);
        }

        public static string[] Split(this string str, char seperator, int count, StringSplitOptions options)
        {
            return str.Split(seperator.ToSingleEntryArray(), count, options);
        }
#endif

    }

}
