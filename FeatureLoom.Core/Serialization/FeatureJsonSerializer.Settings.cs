﻿using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static FeatureLoom.Serialization.FeatureJsonSerializer;

namespace FeatureLoom.Serialization
{

    public sealed partial class FeatureJsonSerializer
    {
        public class Settings
        {
            public TypeInfoHandling typeInfoHandling = TypeInfoHandling.AddDeviatingTypeInfo;
            public DataSelection dataSelection = DataSelection.PublicAndPrivateFields_CleanBackingFields;
            public ReferenceCheck referenceCheck = ReferenceCheck.NoRefCheck;
            public bool enumAsString = false;
            public bool treatEnumerablesAsCollections = true;
            public int writeBufferChunkSize = 64 * 1024;
            public int tempBufferSize = 8 * 1024;
            public bool indent = false;
            public int maxIndentationDepth = 50;
            public int indentationFactor = 2;
            public bool writeByteArrayAsBase64String = true;
            public List<ITypeHandlerCreator> customTypeHandlerCreators = new List<ITypeHandlerCreator>();

            public void AddCustomTypeHandlerCreator<T>(JsonDataTypeCategory category, Func<ExtensionApi, ItemHandler<T>> creator, bool onlyExactType = true)
            {
                customTypeHandlerCreators.Add(new TypeHandlerCreator<T>(category, creator, onlyExactType));
            }

            public void AddCustomTypeHandlerCreator<T>(Func<Type, bool> supportsType, JsonDataTypeCategory category, Func<ExtensionApi, ItemHandler<T>> creator)
            {
                customTypeHandlerCreators.Add(new TypeHandlerCreator<T>(category, creator, supportsType));
            }

            public void AddCustomTypeHandlerCreator(ITypeHandlerCreator creator)
            {
                customTypeHandlerCreators.Add(creator);
            }
        }

        public enum DataSelection
        {
            PublicAndPrivateFields = 0,
            PublicAndPrivateFields_CleanBackingFields = 1,
            PublicAndPrivateFields_RemoveBackingFields = 2,
            PublicFieldsAndProperties = 3,
        }

        public enum ReferenceCheck
        {
            NoRefCheck = 0,
            OnLoopThrowException = 1,
            OnLoopReplaceByNull = 2,
            OnLoopReplaceByRef = 3,
            AlwaysReplaceByRef = 4
        }

        public enum TypeInfoHandling
        {
            AddNoTypeInfo = 0,
            AddDeviatingTypeInfo = 1,
            AddAllTypeInfo = 2,
        }

        private readonly struct CompiledSettings
        {
            public readonly TypeInfoHandling typeInfoHandling;
            public readonly DataSelection dataSelection;
            public readonly ReferenceCheck referenceCheck;
            public readonly bool enumAsString;
            public readonly bool treatEnumerablesAsCollections;
            public readonly int writeBufferChunkSize;
            public readonly int tempBufferSize;
            public readonly bool indent;
            public readonly int maxIndentationDepth;
            public readonly int indentationFactor;
            public readonly ITypeHandlerCreator[] itemHandlerCreators;

            public readonly bool requiresItemNames;
            public readonly bool requiresItemInfos;
            public readonly bool writeByteArrayAsBase64String = false;

            public CompiledSettings(Settings settings)
            {
                typeInfoHandling = settings.typeInfoHandling;
                dataSelection = settings.dataSelection;
                referenceCheck = settings.referenceCheck;
                enumAsString = settings.enumAsString;
                treatEnumerablesAsCollections = settings.treatEnumerablesAsCollections;
                writeBufferChunkSize = settings.writeBufferChunkSize;
                tempBufferSize = settings.tempBufferSize;
                indent = settings.indent;
                maxIndentationDepth = settings.maxIndentationDepth;
                indentationFactor = settings.indentationFactor;
                itemHandlerCreators = settings.customTypeHandlerCreators.Where(creator => creator != null).ToArray();

                requiresItemNames = referenceCheck == ReferenceCheck.AlwaysReplaceByRef || referenceCheck == ReferenceCheck.OnLoopReplaceByRef;
                requiresItemInfos = referenceCheck != ReferenceCheck.NoRefCheck;
                writeByteArrayAsBase64String = settings.writeByteArrayAsBase64String;
            }

        }

    }

    
}
