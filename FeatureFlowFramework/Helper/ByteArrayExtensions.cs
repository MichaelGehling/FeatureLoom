using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public static class ByteArrayExtensions
    {
        public static int FindPatternPos(this byte[] buffer, byte[] pattern)
        {
            return buffer.FindPatternPos(0, buffer.Length, pattern);
        }
    }
}
