using System;
using System.IO;
using System.Text;

namespace FeatureLoom.Extensions
{
    public static class ByteArrayExtensions
    {
        public static T[] CopySection<T>(this T[] data, int offset, int count)
        {
            T[] section = new T[count];
            Array.Copy(data, offset, section, 0, count);
            return section;
        }

        public static int FindPatternPos(this byte[] buffer, byte[] pattern)
        {
            return buffer.FindPatternPos(0, buffer.Length, pattern);
        }

        public static int FindPatternPos(this byte[] buffer, int startIndex, int count, byte[] pattern)
        {
            int patternLen = pattern.Length;
            int bufLen = buffer.Length;
            int patternPos = -1;
            for (int i = startIndex; i < startIndex + count; i++)
            {
                for (int j = 0; j < patternLen && i + j < bufLen; j++)
                {
                    if (buffer[i + j] != pattern[j]) break;
                    else if (j == patternLen - 1) patternPos = i;
                }
                if (patternPos >= 0) break;
            }
            return patternPos;
        }

        public static byte[] Combine(this byte[] array1, byte[] array2)
        {
            byte[] result = new byte[array1.Length + array2.Length];
            Array.Copy(array1, 0, result, 0, array1.Length);
            Array.Copy(array2, 0, result, array1.Length, array2.Length);
            return result;
        }

        public static byte[] Combine(this byte[] array1, byte[] array2, int length)
        {
            byte[] result = new byte[array1.Length + length];
            Array.Copy(array1, 0, result, 0, array1.Length);
            Array.Copy(array2, 0, result, array1.Length, length);
            return result;
        }

        public static string GetString(this byte[] bytes, Encoding encoding = default)
        {
            if (encoding == default) encoding = Encoding.UTF8;
            return encoding.GetString(bytes);
        }

        public static string GetStringOrNull(this byte[] bytes, Encoding encoding = default)
        {
            try
            {
                if (encoding == default) encoding = Encoding.UTF8;
                return encoding.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        public static Stream ToStream(this byte[] buffer)
        {
            return new MemoryStream(buffer);
        }
        
        public static bool CompareTo(this byte[] self, byte[] other, int ownOffset = 0, int otherOffset = 0, int length = -1)
        {
            if (self == other) return true;
            if (self == null || other == null) return false;
            if (length == -1)
            {
                if (self.Length - ownOffset != other.Length - otherOffset) return false;
                length = self.Length - ownOffset;
            }
            if (self.Length < ownOffset + length) throw new ArgumentException("Offset + length exceeds own length!");
            if (other.Length < otherOffset + length) return false;

            for(int i=0; i < length; i++)
            {
                if (self[i + ownOffset] != other[i + otherOffset]) return false;
            }
            return true;
        }

    }
}