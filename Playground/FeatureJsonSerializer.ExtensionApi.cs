using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        public sealed class ExtensionApi
        {
            readonly FeatureJsonSerializer s;
            readonly JsonUTF8StreamWriter w;

            public ExtensionApi(FeatureJsonSerializer serializer)
            {
                this.s = serializer;
                this.w = serializer.writer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public CachedTypeHandler GetCachedTypeHandler(Type type) => s.GetCachedTypeHandler(type);

            public IWriter Writer => w;       
        }

    }
}
