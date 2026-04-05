using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    public sealed class ExtensionApi
    {
        readonly JsonSerializer serializer;
        readonly JsonUTF8StreamWriter writer;            

        public ExtensionApi(JsonSerializer serializer)
        {
            this.serializer = serializer;
            this.writer = serializer.writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CachedTypeHandler GetCachedTypeHandler(Type type) => serializer.GetCachedTypeHandler(type);

        public IWriter Writer => writer;
        public bool RequiresItemNames => serializer.settings.requiresItemNames;
        public bool RequiresHandler => serializer.settings.requiresItemInfos;
    }

}
