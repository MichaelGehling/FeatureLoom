using System;
using System.Collections.Generic;
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

#if NETSTANDARD2_0
        public static void CopyTo<T>(this ArraySegment<T> source, T[] destination, int destinationIndex)
        {
            // Check for null arguments
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (destinationIndex < 0 || destinationIndex >= destination.Length)
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            if (source.Count + destinationIndex > destination.Length)
                throw new ArgumentException("Destination array is not large enough to hold the source data.");

            // Copy elements from source to destination
            Array.Copy(source.Array, source.Offset, destination, destinationIndex, source.Count);
        }
#endif

        public static void CopyFrom<T>(this ArraySegment<T> slice, T[] source, int sourceOffset, int count)
        {
            if (slice.Count < count) throw new ArgumentOutOfRangeException("count");
            Array.Copy(source, sourceOffset, slice.Array, slice.Offset, count);
        }

        public static void CopyFrom<T>(this ArraySegment<T> slice, IList<T> source, int sourceOffset, int count)
        {
            if (slice.Count < count) throw new ArgumentOutOfRangeException("count");
            if (source is T[] sourceArray) CopyFrom(slice, sourceArray, sourceOffset, count);
            else if (source is List<T> sourceList) sourceList.CopyTo(sourceOffset, slice.Array, slice.Offset, count);
            else
            {
                for (int i = 0; i < count; i++)
                {
                    int sourceIndex = sourceOffset + i;
                    slice.Array[i] = source[sourceIndex];
                }
            }
        }

        public static void CopyFrom<T>(this ArraySegment<T> slice, ArraySegment<T> source)
        {
            if (slice.Count < source.Count) throw new ArgumentOutOfRangeException("count");
            Array.Copy(source.Array, source.Offset, slice.Array, slice.Offset, source.Count);
        }

        public static void CopyFrom<T>(this ArraySegment<T> slice, int offset, T[] source, int sourceOffset, int count)
        {
            if (slice.Count < count) throw new ArgumentOutOfRangeException("count");
            Array.Copy(source, sourceOffset, slice.Array, slice.Offset + offset, count);
        }

        public static void CopyFrom<T>(this ArraySegment<T> slice, int offset, IList<T> source, int sourceOffset, int count)
        {
            if (slice.Count < count) throw new ArgumentOutOfRangeException("count");
            if (source is T[] sourceArray) CopyFrom(slice, offset, sourceArray, sourceOffset, count);
            else if (source is List<T> sourceList) sourceList.CopyTo(sourceOffset, slice.Array, offset + slice.Offset, count);
            else
            {
                for (int i = 0; i < count; i++)
                {
                    int sourceIndex = sourceOffset + i;
                    slice.Array[offset + i] = source[sourceIndex];
                }
            }
        }

        public static void CopyFrom<T>(this ArraySegment<T> slice, int offset, ArraySegment<T> source)
        {
            if (slice.Count < source.Count) throw new ArgumentOutOfRangeException("count");
            Array.Copy(source.Array, source.Offset, slice.Array, slice.Offset + offset, source.Count);
        }


        public static T[] ToArray<T>(ArraySegment<T> segment)
        {
            if (segment.Array == null)
                throw new ArgumentNullException(nameof(segment.Array), "Array is null.");

            T[] result = new T[segment.Count];
            Array.Copy(segment.Array, segment.Offset, result, 0, segment.Count);
            return result;
        }

        public static T Get<T>(this ArraySegment<T> segment, int index)
        {
#if NETSTANDARD2_0
            if (segment.Array == null) throw new InvalidOperationException("The underlying array is null.");
            return segment.Array[segment.Offset + index];
#else
            return segment[index];
#endif
        }

        public static void Set<T>(this ArraySegment<T> segment, int index, T value)
        {
#if NETSTANDARD2_0
            if (segment.Array == null) throw new InvalidOperationException("The underlying array is null.");
            segment.Array[segment.Offset + index] = value;
#else
            segment[index] = value;
#endif
        }

        // Slice method for .NET Standard 2.0
        public static ArraySegment<T> Slice<T>(this ArraySegment<T> segment, int index)
        {
#if NETSTANDARD2_0
            if (index < 0 || index > segment.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of the range of the ArraySegment.");
            return new ArraySegment<T>(segment.Array, segment.Offset + index, segment.Count - index);
#else
            // Use the built-in Slice method for .NET Standard 2.1 and later
            return segment.Slice(index);
#endif
        }

        // Slice method for .NET Standard 2.0 with start index and length
        public static ArraySegment<T> Slice<T>(this ArraySegment<T> segment, int index, int length)
        {
#if NETSTANDARD2_0
            if (index < 0 || length < 0 || index + length > segment.Count) throw new ArgumentOutOfRangeException(nameof(index), "Index or length is out of the range of the ArraySegment.");       
            return new ArraySegment<T>(segment.Array, segment.Offset + index, length);
#else
            // Use the built-in Slice method for .NET Standard 2.1 and later
            return segment.Slice(index, length);
#endif
        }

    }
}