using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static FeatureLoom.Serialization.JsonSerializer;

namespace FeatureLoom.Serialization
{

    public sealed partial class JsonSerializer
    {
        public class Settings
        {
            public TypeInfoHandling typeInfoHandling = TypeInfoHandling.AddDeviatingTypeInfo;
            public DataSelection dataSelection = DataSelection.PublicAndPrivateFields_CleanBackingFields;
            public ReferenceCheck referenceCheck = ReferenceCheck.NoRefCheck;
            public ReferenceFormat referenceFormat = ReferenceFormat.JsonPath;
            public bool enumAsString = false;
            public bool treatEnumerablesAsCollections = true;
            public int writeBufferChunkSize = 64 * 1024;
            public int tempBufferSize = 8 * 1024;
            public bool indent = false;
            public int maxIndentationDepth = 50;
            public int indentationFactor = 2;
            public bool writeByteArrayAsBase64String = true;
            public bool writeArraySegmentsAsArrays = true;
            public List<ITypeHandlerCreator> customTypeHandlerCreators = new List<ITypeHandlerCreator>();

            public void AddCustomTypeHandlerCreator<T>(JsonDataTypeCategory category, Func<ExtensionApi, Action<T>> creator, bool onlyExactType = true)
            {
                customTypeHandlerCreators.Add(new TypeHandlerCreator<T>(category, creator, onlyExactType));
            }

            public void AddCustomTypeHandlerCreator<T>(Func<Type, bool> supportsType, JsonDataTypeCategory category, Func<ExtensionApi, Action<T>> creator)
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

        /// <summary>
        /// Determines if and how repeated or circular object references are detected while writing.
        /// <para>
        /// Performance note: the loop detecting modes are not a cheap middle ground between
        /// <see cref="NoRefCheck"/> and <see cref="AlwaysReplaceByRef"/>. They pay the full
        /// bookkeeping cost (item infos and, in <see cref="ReferenceFormat.JsonPath"/> mode, item
        /// names) but only shrink the output when a loop is actually present. On acyclic graphs
        /// <see cref="AlwaysReplaceByRef"/> is therefore typically faster, because writing repeated
        /// objects as refs saves more output than the tracking costs.
        /// See SerializeReferenceHandlingTest in FeatureLoom.PerformanceTests.
        /// </para>
        /// </summary>
        public enum ReferenceCheck
        {
            /// <summary>
            /// No reference tracking at all. Fastest option, but shared objects are written
            /// repeatedly and circular references cause an endless loop / stack overflow.
            /// Only use this if the object graph is known to be a tree.
            /// </summary>
            NoRefCheck = 0,

            /// <summary>
            /// Detects circular references and throws an exception instead of writing them.
            /// </summary>
            OnLoopThrowException = 1,

            /// <summary>
            /// Detects circular references and writes null in place of the looping object,
            /// breaking the cycle without adding any ref syntax to the output.
            /// </summary>
            OnLoopReplaceByNull = 2,

            /// <summary>
            /// Detects circular references and writes a ref (see <see cref="ReferenceFormat"/>) in
            /// place of the looping object. Repeated but non-circular occurrences are still written
            /// out in full.
            /// <para>
            /// Beware: on graphs without loops this produces exactly the same output as
            /// <see cref="NoRefCheck"/> while still paying the tracking overhead, which makes it
            /// the slowest mode in that case. Prefer <see cref="AlwaysReplaceByRef"/> unless
            /// duplicated objects really have to be materialized as separate instances.
            /// </para>
            /// </summary>
            OnLoopReplaceByRef = 3,

            /// <summary>
            /// Every object is written only once; each repeated occurrence becomes a ref (see
            /// <see cref="ReferenceFormat"/>). This preserves object identity on deserialization
            /// and usually shrinks the output noticeably, which often compensates the tracking cost.
            /// </summary>
            AlwaysReplaceByRef = 4
        }

        /// <summary>
        /// Determines how references to already serialized objects are represented.
        /// </summary>
        public enum ReferenceFormat
        {
            /// <summary>
            /// References are written as a JSONPath pointing at the location of the first
            /// occurrence, e.g. {"$ref":"$.Items[0]"}. This produces clean, human readable output
            /// and requires no additional members on the referenced objects, but it is specific to
            /// this serializer.
            /// <para>
            /// This format needs the name of every written item to build the paths, so it is
            /// slightly slower than <see cref="IdBased"/> even though it produces less output.
            /// </para>
            /// </summary>
            JsonPath = 0,

            /// <summary>
            /// References use the id based format established by System.Text.Json
            /// (ReferenceHandler.Preserve) and Newtonsoft.Json (PreserveReferencesHandling).
            /// Every reference tracked object gets an "$id" member and repeated occurrences are
            /// written as {"$ref":"1"}. Arrays are wrapped as {"$id":"1","$values":[...]}.
            /// Use this mode when the JSON has to be read by those serializers.
            /// <para>
            /// Writes a few more bytes than <see cref="JsonPath"/>, but is faster because no item
            /// names have to be tracked.
            /// </para>
            /// </summary>
            IdBased = 1
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
            public readonly ReferenceFormat referenceFormat;
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
            public readonly bool writeItemIds;
            public readonly bool writeByteArrayAsBase64String = false;
            public readonly bool writeArraySegmentsAsArrays = false;

            public CompiledSettings(Settings settings)
            {
                typeInfoHandling = settings.typeInfoHandling;
                dataSelection = settings.dataSelection;
                referenceCheck = settings.referenceCheck;
                referenceFormat = settings.referenceFormat;
                enumAsString = settings.enumAsString;
                treatEnumerablesAsCollections = settings.treatEnumerablesAsCollections;
                writeBufferChunkSize = settings.writeBufferChunkSize;
                tempBufferSize = settings.tempBufferSize;
                indent = settings.indent;
                maxIndentationDepth = settings.maxIndentationDepth;
                indentationFactor = settings.indentationFactor;
                itemHandlerCreators = settings.customTypeHandlerCreators.Where(creator => creator != null).ToArray();

                // Item names are only needed to build JSONPath ref values. The id based format
                // identifies objects by an explicit "$id" member, so the path is irrelevant there.
                requiresItemNames = referenceFormat == ReferenceFormat.JsonPath &&
                                    (referenceCheck == ReferenceCheck.AlwaysReplaceByRef || referenceCheck == ReferenceCheck.OnLoopReplaceByRef);
                requiresItemInfos = referenceCheck != ReferenceCheck.NoRefCheck;
                writeItemIds = referenceFormat == ReferenceFormat.IdBased &&
                               (referenceCheck == ReferenceCheck.AlwaysReplaceByRef || referenceCheck == ReferenceCheck.OnLoopReplaceByRef);
                writeByteArrayAsBase64String = settings.writeByteArrayAsBase64String;
                writeArraySegmentsAsArrays = settings.writeArraySegmentsAsArrays;
            }

        }

    }

    
}
