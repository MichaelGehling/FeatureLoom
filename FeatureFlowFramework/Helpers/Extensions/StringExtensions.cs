using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FeatureFlowFramework.Helpers.Extensions
{
    public static class StringExtensions
    {
        public static bool StartsWith(this string str, char c)
        {
            return !str.EmptyOrNull() && str[0] == c;
        }

        public static bool Contains(this string str, char c)
        {
            foreach(var sc in str)
            {
                if(sc == c) return true;
            }
            return false;
        }

        public static byte[] ToByteArray(this string str, Encoding encoding = default)
        {
            if(encoding == default) encoding = Encoding.UTF8;
            return encoding.GetBytes(str);
        }

        public static string GetString(this byte[] bytes, Encoding encoding = default)
        {
            if(encoding == default) encoding = Encoding.UTF8;
            return encoding.GetString(bytes);
        }

        public static string AddToPath(this string pathBase, string pathExtension, char seperator = '\\')
        {
            string temp = pathBase;
            if((pathBase.Length > 0 && pathBase.Last() != seperator) &&
                (pathExtension.Length == 0 || pathExtension.First() != seperator)) temp += seperator;
            temp += pathExtension;
            return temp;
        }

        public static int FindPatternPos(this byte[] buffer, int startIndex, int count, byte[] pattern)
        {
            int patternLen = pattern.Length;
            int bufLen = buffer.Length;
            int patternPos = -1;
            for(int i = startIndex; i < startIndex + count; i++)
            {
                for(int j = 0; j < patternLen && i + j < bufLen; j++)
                {
                    if(buffer[i + j] != pattern[j]) break;
                    else if(j == patternLen - 1) patternPos = i;
                }
                if(patternPos >= 0) break;
            }
            return patternPos;
        }

        

        public static string MakeValidFilename(this string fileName)
        {
            foreach(char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        public static string MakeValidFilePath(this string fielPath)
        {
            foreach(char c in Path.GetInvalidPathChars())
            {
                fielPath = fielPath.Replace(c, '_');
            }
            return fielPath;
        }

        public static string TextWrap(this string input, int maxCharsPerLine, string nextLine)
        {
            if(input == null) return null;
            string result = "";
            bool whiteSpaceFound = false;
            List<int> potentialBreaks = new List<int>();
            for(int i = 0; i < input.Length; i++)
            {
                if(char.IsWhiteSpace(input[i])) whiteSpaceFound = true;
                else if(whiteSpaceFound)
                {
                    potentialBreaks.Add(i);
                    whiteSpaceFound = false;
                }
            }
            int lastBreak = 0;
            for(int i = 0; i < potentialBreaks.Count; i++)
            {
                if(i + 1 == potentialBreaks.Count)
                {
                    if(input.Length - lastBreak > maxCharsPerLine)
                    {
                        result += input.Substring(lastBreak, potentialBreaks[i] - lastBreak) + nextLine;
                        lastBreak = potentialBreaks[i];
                    }
                }
                else
                {
                    if(potentialBreaks[i + 1] - lastBreak > maxCharsPerLine)
                    {
                        result += input.Substring(lastBreak, potentialBreaks[i] - lastBreak) + nextLine;
                        lastBreak = potentialBreaks[i];
                    }
                }
            }
            result += input.Substring(lastBreak);
            return result;
        }

        public static string TrimEnd(this string str, string trimStr)
        {
            // TODO: Not very efficient when trim is performed multiple times... improve!
            while(str.EndsWith(trimStr))
            {
                str = str.Substring(0, str.Length - trimStr.Length);
            }
            return str;
        }

        public static string Substring(this string str, string startAfter, string endBefore = null)
        {
            int startPos = 0;
            int endPos = str.Length-1;
            if (!startAfter.EmptyOrNull())
            {
                startPos = str.IndexOf(startAfter) + startAfter.Length;
            }
            if (!endBefore.EmptyOrNull())
            {
                endPos = str.IndexOf(endBefore, startPos);
            }
            return str.Substring(startPos, endPos - startPos + 1);
        }
    }
}