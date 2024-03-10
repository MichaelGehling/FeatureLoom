using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Playground.FeatureJsonSerializer;

namespace Playground
{

    public sealed partial class FeatureJsonSerializer
    {
        public class Settings
        {
            public TypeInfoHandling typeInfoHandling = TypeInfoHandling.AddDeviatingTypeInfo;
            public DataSelection dataSelection = DataSelection.PublicAndPrivateFields_CleanBackingFields;
            public ReferenceCheck referenceCheck = ReferenceCheck.AlwaysReplaceByRef;
            public int bufferSize = -1;
            public bool enumAsString = false;
            public bool treatEnumerablesAsCollections = true;
            public int writeBufferChunkSize = 64 * 1024;
            public int tempBufferSize = 8 * 1024;
            public List<ITypeHandlerCreator> itemHandlerCreators = new List<ITypeHandlerCreator>();

            public void AddCustomTypeHandlerCreator<T>(JsonDataTypeCategory category, Func<ExtensionApi, ItemHandler<T>> creator)
            {
                itemHandlerCreators.Add(new TypeHandlerCreator<T>(category, creator));
            }
        }

        public enum DataSelection
        {
            PublicAndPrivateFields = 0,
            PublicAndPrivateFields_CleanBackingFields = 1,
            PublicFieldsAndProperties = 2,
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
            public readonly int bufferSize;
            public readonly bool enumAsString;
            public readonly bool treatEnumerablesAsCollections;
            public readonly int writeBufferChunkSize;
            public readonly int tempBufferSize;
            public readonly ITypeHandlerCreator[] itemHandlerCreators;

            public readonly bool requiresItemNames;
            public readonly bool requiresItemInfos;

            public CompiledSettings(Settings settings)
            {
                typeInfoHandling = settings.typeInfoHandling;
                dataSelection = settings.dataSelection;
                referenceCheck = settings.referenceCheck;
                bufferSize = settings.bufferSize;
                enumAsString = settings.enumAsString;
                treatEnumerablesAsCollections = settings.treatEnumerablesAsCollections;
                writeBufferChunkSize = settings.writeBufferChunkSize;
                tempBufferSize = settings.tempBufferSize;
                itemHandlerCreators = settings.itemHandlerCreators.Where(creator => creator != null).ToArray();

                requiresItemNames = referenceCheck == ReferenceCheck.AlwaysReplaceByRef || referenceCheck == ReferenceCheck.OnLoopReplaceByRef;
                requiresItemInfos = referenceCheck != ReferenceCheck.NoRefCheck;
            }

        }

    }

    
}
