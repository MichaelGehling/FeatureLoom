using FeatureLoom.Extensions;
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
        public CachedTypeWriter GetCachedTypeHandler(Type type) => serializer.GetCachedTypeWriter(type);

        /// <summary>
        /// Wraps a custom item handler (which only writes the item's body) into a complete item writer
        /// matching the given category and applies it to the type handler.
        /// </summary>
        public void SetItemHandler<T>(ICachedTypeHandler cachedTypeHandler, Action<T> itemHandler, JsonDataTypeCategory category, Type handlerType)
        {
            if (cachedTypeHandler is not CachedTypeWriter typeWriter) throw new ArgumentException($"Unsupported type handler implementation {cachedTypeHandler?.GetType().FullName}", nameof(cachedTypeHandler));
            if (!handlerType.IsAssignableTo(typeof(T))) throw new ArgumentException($"The provided item handler for type {typeof(T).FullName} is not compatible with the actual item type {handlerType.FullName}");

            switch (category)
            {
                case JsonDataTypeCategory.Primitive:
                    typeWriter.SetItemWriter(serializer.CreatePrimitiveItemWriter(typeWriter, itemHandler), false);
                    // A primitive never has children, so it can never contain a reference path,
                    // even when the handled type itself is a reference type (e.g. string).
                    typeWriter.ForceNoRefTypes();
                    break;
                case JsonDataTypeCategory.Array: typeWriter.SetItemWriter(serializer.CreateArrayItemWriter(typeWriter, itemHandler), true); break;
                case JsonDataTypeCategory.Array_WithoutRefChildren: typeWriter.SetItemWriter(serializer.CreateArrayItemWriter(typeWriter, itemHandler), false); break;
                case JsonDataTypeCategory.Object: typeWriter.SetItemWriter(serializer.CreateObjectItemWriter(typeWriter, itemHandler), true); break;
                case JsonDataTypeCategory.Object_WithoutRefChildren: typeWriter.SetItemWriter(serializer.CreateObjectItemWriter(typeWriter, itemHandler), false); break;
            }
            typeWriter.OverrideHandlerType(handlerType);
        }

        public IWriter Writer => writer;
        public bool RequiresItemNames => serializer.settings.requiresItemNames;
        public bool RequiresHandler => serializer.settings.requiresItemInfos;
    }

}
