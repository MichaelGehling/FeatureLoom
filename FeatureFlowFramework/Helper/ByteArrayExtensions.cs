using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public static class ByteArrayExtensions
    {
        public static int FindPatternPos(this byte[] buffer, byte[] pattern)
        {
            return buffer.FindPatternPos(0, buffer.Length, pattern);
        }

        public static byte[] Combine(this byte[] array1, byte[] array2)
        {
            byte[] result = new byte[array1.Length + array2.Length];
            System.Buffer.BlockCopy(array1, 0, result, 0, array1.Length);
            System.Buffer.BlockCopy(array2, 0, result, array1.Length, array2.Length);            
            return result;
        }
        public static Stream ToStream(this byte[] buffer)
        {
            return new MemoryStream(buffer);
        }
    }
}
