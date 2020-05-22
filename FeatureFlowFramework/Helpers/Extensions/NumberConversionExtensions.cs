using System;

namespace FeatureFlowFramework.Helpers.Extensions
{
    public static class NumberConversionExtensions
    {
        public static double ToDouble(this long value)
        {
            return value;
        }

        public static byte[] ToBytes(this int number, bool littleEndian)
        {
            byte[] bytes = BitConverter.GetBytes(number);
            if(!BitConverter.IsLittleEndian && littleEndian) Array.Reverse(bytes);
            return bytes;
        }

        public static int ToInt32(this byte[] bytes, int startIndex, bool littleEndian)
        {
            if(!BitConverter.IsLittleEndian && littleEndian)
            {
                int len = sizeof(int);
                int maxIdx = len - 1;
                byte[] part = new byte[len];
                for(int i = 0; i <= len; i++)
                {
                    part[maxIdx - i] = bytes[startIndex + i];
                }
                bytes = part;
            }
            return BitConverter.ToInt32(bytes, startIndex);
        }

        public static int ToIntTruncated(this double d)
        {
            if(d > int.MaxValue) return int.MaxValue;
            else if(d < int.MinValue) return int.MinValue;
            else return (int)d;
        }
    }
}