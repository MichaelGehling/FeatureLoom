using System;
using System.IO;
using System.Text;

namespace FeatureLoom.Extensions
{
    public static class ByteArrayExtensions
    {
        public static T[] Slice<T>(this T[] data, int offset, int count)
        {
            T[] slice = new T[count];
            Buffer.BlockCopy(data, offset, slice, 0, count);
            return slice;
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
            System.Buffer.BlockCopy(array1, 0, result, 0, array1.Length);
            System.Buffer.BlockCopy(array2, 0, result, array1.Length, array2.Length);
            return result;
        }

        public static byte[] Combine(this byte[] array1, byte[] array2, int length)
        {
            byte[] result = new byte[array1.Length + length];
            System.Buffer.BlockCopy(array1, 0, result, 0, array1.Length);
            System.Buffer.BlockCopy(array2, 0, result, array1.Length, length);
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

    }
}