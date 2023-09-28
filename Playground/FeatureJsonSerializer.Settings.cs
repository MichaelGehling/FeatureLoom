using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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

            public bool RequiresItemNames => referenceCheck == ReferenceCheck.AlwaysReplaceByRef || referenceCheck == ReferenceCheck.OnLoopReplaceByRef;
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
        
    }
}
