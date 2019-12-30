using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FeatureFlowFramework.Helper
{
    public static class StringGenerator
    {
        public static string CallerInfo([System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                                        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                                        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            return $"Method: {caller}, File: {sourceFile}, Line: {sourceLine}";
        }
    }
    
}
