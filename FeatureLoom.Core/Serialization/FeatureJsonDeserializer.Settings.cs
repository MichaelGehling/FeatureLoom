using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using static FeatureLoom.Serialization.FeatureJsonDeserializer.Settings;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{
    public class Settings
    {
        public DataAccess dataAccess = DataAccess.PublicAndPrivateFields;
        public Dictionary<Type, object> constructors = new();
        public Dictionary<(Type, Type), object> constructorsWithParam = new();
        public Dictionary<Type, Type> typeMapping = new();
        public Dictionary<Type, Type[]> multiOptionTypeMapping = new();
        public Dictionary<Type, Type> genericTypeMapping = new();
        public Dictionary<Type, object> customTypeReaders = new();
        public Dictionary<string, Type> customTypeNames = new();
        public HashSet<Type> forbiddenTypes = new();

        public bool enableReferenceResolution = false;
        public bool enableStringRefResolution = false;
        public ProposedTypeHandling proposedTypeHandling = ProposedTypeHandling.CheckWhereReasonable;

        // If true, when custom type names are loaded into the proposed-type cache,
        // lower/upper-case variants are added too.
        public bool addCaseVariantsForCustomTypeNames = false;

        public int initialBufferSize = 1024 * 64;
        public bool tryCastArraysOfUnknownValues = true;
        public bool rethrowExceptions = false;
        public bool logCatchedExceptions = true;
        public bool strict = false;
        public bool populateExistingMembers = false;
        public bool useStringCache = false;
        public int stringCacheBitSize = 12; //4096 entries
        public int stringCacheMaxLength = 128; //max string length to be cached
        public bool allowUninitializedObjectCreation = false;

        public TypeWhitelistMode typeWhitelistMode = TypeWhitelistMode.Disabled;
        public HashSet<Type> allowedTypes = new();
        public HashSet<string> allowedNamespacePrefixes = new(StringComparer.Ordinal);

        public Settings()
        {
            AddTypeMapping(typeof(IEnumerable), typeof(List<object>));
            AddTypeMapping(typeof(ICollection), typeof(List<object>));
            AddTypeMapping(typeof(IList), typeof(List<object>));

            AddGenericTypeMapping(typeof(IEnumerable<>), typeof(List<>));
            AddGenericTypeMapping(typeof(ICollection<>), typeof(List<>));
            AddGenericTypeMapping(typeof(IReadOnlyCollection<>), typeof(List<>));
            AddGenericTypeMapping(typeof(IList<>), typeof(List<>));
            AddGenericTypeMapping(typeof(IReadOnlyList<>), typeof(List<>));
            AddGenericTypeMapping(typeof(IDictionary<,>), typeof(Dictionary<,>));
            AddGenericTypeMapping(typeof(IReadOnlyDictionary<,>), typeof(Dictionary<,>));
            AddGenericTypeMapping(typeof(ISet<>), typeof(HashSet<>));
            AddGenericTypeMapping(typeof(IProducerConsumerCollection<>), typeof(ConcurrentQueue<>));

            AddDefaultForbiddenTypes();

            AddDefaultCustomTypeNames();
            AddCSharpKeywordTypeNames();
            AddCommonCrossLanguageTypeNames();
        }

        private void AddDefaultForbiddenTypes()
        {
            // Reflection / runtime metadata
            AddForbiddenType(typeof(Type));
            AddForbiddenType(typeof(System.Reflection.Assembly));
            AddForbiddenType(typeof(System.Reflection.Module));
            AddForbiddenType(typeof(System.Reflection.MemberInfo));
            AddForbiddenType(typeof(System.Reflection.MethodInfo));
            AddForbiddenType(typeof(System.Reflection.ConstructorInfo));
            AddForbiddenType(typeof(System.Reflection.FieldInfo));
            AddForbiddenType(typeof(System.Reflection.PropertyInfo));
            AddForbiddenType(typeof(System.Reflection.EventInfo));

            // Delegate / expression trees
            AddForbiddenType(typeof(Delegate));
            AddForbiddenType(typeof(MulticastDelegate));
            AddForbiddenType(typeof(System.Linq.Expressions.Expression));
            AddForbiddenType(typeof(System.Linq.Expressions.LambdaExpression));
            AddForbiddenType(typeof(System.Linq.Expressions.Expression<>)); // generic definition

            // Process / OS interaction
            AddForbiddenType(typeof(System.Diagnostics.Process));
            AddForbiddenType(typeof(System.Diagnostics.ProcessStartInfo));

            // File system handles/abstractions
            AddForbiddenType(typeof(System.IO.FileSystemInfo));
            AddForbiddenType(typeof(System.IO.FileInfo));
            AddForbiddenType(typeof(System.IO.DirectoryInfo));
            AddForbiddenType(typeof(System.IO.DriveInfo));

            // Threading primitives
            AddForbiddenType(typeof(System.Threading.Thread));
        }

        public void AddDefaultCustomTypeNames()
        {
            // existing short CLR names
            AddCustomTypeName(typeof(string).Name, typeof(string));
            AddCustomTypeName(typeof(long).Name, typeof(long));
            AddCustomTypeName(typeof(ulong).Name, typeof(ulong));
            AddCustomTypeName(typeof(int).Name, typeof(int));
            AddCustomTypeName(typeof(uint).Name, typeof(uint));
            AddCustomTypeName(typeof(short).Name, typeof(short));
            AddCustomTypeName(typeof(ushort).Name, typeof(ushort));
            AddCustomTypeName(typeof(byte).Name, typeof(byte));
            AddCustomTypeName(typeof(sbyte).Name, typeof(sbyte));
            AddCustomTypeName(typeof(bool).Name, typeof(bool));
            AddCustomTypeName(typeof(char).Name, typeof(char));
            AddCustomTypeName(typeof(float).Name, typeof(float));
            AddCustomTypeName(typeof(double).Name, typeof(double));
            AddCustomTypeName(typeof(decimal).Name, typeof(decimal));
            AddCustomTypeName(typeof(DateTime).Name, typeof(DateTime));
            AddCustomTypeName(typeof(TimeSpan).Name, typeof(TimeSpan));
            AddCustomTypeName(typeof(Guid).Name, typeof(Guid));
            AddCustomTypeName(typeof(DateTimeOffset).Name, typeof(DateTimeOffset));
            AddCustomTypeName(typeof(Uri).Name, typeof(Uri));                
            AddCustomTypeName(typeof(byte[]).Name, typeof(byte[]));
        }

        public void AddCSharpKeywordTypeNames()
        {
            AddCustomTypeName("string", typeof(string));
            AddCustomTypeName("long", typeof(long));
            AddCustomTypeName("ulong", typeof(ulong));
            AddCustomTypeName("int", typeof(int));
            AddCustomTypeName("uint", typeof(uint));
            AddCustomTypeName("short", typeof(short));
            AddCustomTypeName("ushort", typeof(ushort));
            AddCustomTypeName("byte", typeof(byte));
            AddCustomTypeName("sbyte", typeof(sbyte));
            AddCustomTypeName("bool", typeof(bool));
            AddCustomTypeName("char", typeof(char));
            AddCustomTypeName("float", typeof(float));
            AddCustomTypeName("double", typeof(double));
            AddCustomTypeName("decimal", typeof(decimal));
        }

        public void AddCommonCrossLanguageTypeNames()
        {
            AddCustomTypeName("boolean", typeof(bool));
            AddCustomTypeName("datetime", typeof(DateTime));
            AddCustomTypeName("timestamp", typeof(DateTimeOffset));
            AddCustomTypeName("duration", typeof(TimeSpan));
            AddCustomTypeName("uuid", typeof(Guid));
            AddCustomTypeName("uri", typeof(Uri));
            AddCustomTypeName("bytes", typeof(byte[]));
            AddCustomTypeName("binary", typeof(byte[]));
        }

        public void ClearCustomTypeNames() => customTypeNames.Clear();

        public void AddConstructor<T>(Func<T> constructor) => constructors[typeof(T)] = constructor;
        public void AddConstructorWithParameter<T, P>(Func<P, T> constructor) => constructorsWithParam[(typeof(T), typeof(P))] = constructor;
        public void AddTypeMapping(Type baseType, Type mappedType)
        {
            if (!mappedType.IsAssignableTo(baseType)) throw new Exception($"{TypeNameHelper.Shared.GetSimplifiedTypeName(baseType)} is not implemented by {TypeNameHelper.Shared.GetSimplifiedTypeName(mappedType)}");
            typeMapping[baseType] = mappedType;
        }
        public void AddGenericTypeMapping(Type genericBaseType, Type genericImplType)
        {
            if (!genericImplType.IsOfGenericType(genericBaseType)) throw new Exception($"{TypeNameHelper.Shared.GetSimplifiedTypeName(genericBaseType)} is not implemented by {TypeNameHelper.Shared.GetSimplifiedTypeName(genericImplType)}");
            genericTypeMapping[genericBaseType] = genericImplType;
        }

        public void AddMultiOptionTypeMapping(Type baseType, params Type[] typeOptions)
        {
            foreach (var typeOption in typeOptions)
            {
                if (!typeOption.IsAssignableTo(baseType)) throw new Exception($"{TypeNameHelper.Shared.GetSimplifiedTypeName(baseType)} is not implemented by {TypeNameHelper.Shared.GetSimplifiedTypeName(typeOption)}");
            }
            multiOptionTypeMapping[baseType] = typeOptions;
        }

        public void AddCustomTypeReader<T>(ICustomTypeReader<T> customTypeReader)
        {
            customTypeReaders[typeof(T)] = customTypeReader;
        }

        public void AddCustomTypeReader<T>(Func<ExtensionApi, T> readType)
        {
            AddCustomTypeReader<T>(new CustomTypeReader<T>(readType));
        }

        public void AddCustomTypeName(string customTypeName, Type type)
        {
            customTypeNames[customTypeName] = type;
        }

        public void AddForbiddenType(Type type)
        {
            forbiddenTypes.Add(type);
        }

        public enum TypeWhitelistMode
        {
            Disabled = 0,
            ForProposedTypesOnly = 1,
            ForAllNonIntrinsicTypes = 2
        } 
        
        public enum ProposedTypeHandling
        {
            /// <summary>
            /// Proposed types (e.g. from $type properties in JSON) are ignored and not used for deserialization. 
            /// This is the safest option when deserializing untrusted data, but also means that polymorphic deserialization based on type information in the JSON will not work.
            /// This option allows for the hightest performance.
            /// If primitive values and arrays are wrapped in a $type object, the deserializer will fail.
            /// </summary>
            Ignore = 0,
            /// <summary>
            /// Proposed types are ignored where they don't make any sense with the given target type, e.g. primitive values and sealed types, 
            /// but used where they can be applied, e.g. for deserializing into an interface, abstract class or base class.
            /// This option provides a good balance between performance and flexibility.            
            /// </summary>
            CheckWhereReasonable = 1,
            /// <summary>
            /// Proposed types are always checked and used for deserialization if they are present, regardless of the target type.
            /// This option has a significant performance impact, but rarely used in practice, as it allows for proposed types to be applied in situations 
            /// where they don't make much sense.
            /// </summary>
            CheckAlways = 2
        }

        public void AddAllowedType(Type type) => allowedTypes.Add(type);
        public void AddAllowedType<T>() => allowedTypes.Add(typeof(T));
        public void AddAllowedNamespacePrefix(string prefix)
        {
            if (!string.IsNullOrWhiteSpace(prefix)) allowedNamespacePrefixes.Add(prefix);
        }
    }

    private readonly struct CompiledSettings
    {
        public readonly DataAccess dataAccess;
        public readonly Dictionary<Type, object> constructors;
        public readonly Dictionary<(Type, Type), object> constructorsWithParam;
        public readonly Dictionary<Type, Type> typeMapping;
        public readonly Dictionary<Type, Type[]> multiOptionTypeMapping;
        public readonly Dictionary<Type, Type> genericTypeMapping;
        public readonly Dictionary<Type, object> customTypeReaders;
        public readonly Dictionary<string, Type> customTypeNames;
        public readonly HashSet<Type> forbiddenTypes;

        public readonly bool enableReferenceResolution;
        public readonly bool enableStringRefResolution;
        public readonly ProposedTypeHandling proposedTypeHandling;
        public readonly bool addCaseVariantsForCustomTypeNames;

        public readonly int initialBufferSize;
        public readonly bool tryCastArraysOfUnknownValues;
        public readonly bool rethrowExceptions;
        public readonly bool logCatchedExceptions;
        public readonly bool strict;
        public readonly bool populateExistingMembers;
        public readonly bool useStringCache;
        public readonly int stringCacheBitSize;
        public readonly int stringCacheMaxLength;
        public readonly bool allowUninitializedObjectCreation;

        public readonly Settings.TypeWhitelistMode typeWhitelistMode;
        public readonly HashSet<Type> allowedTypes;
        public readonly HashSet<string> allowedNamespacePrefixes;

        public CompiledSettings(Settings settings)
        {
            dataAccess = settings.dataAccess;

            constructors = new (settings.constructors);
            constructorsWithParam = new (settings.constructorsWithParam);
            typeMapping = new (settings.typeMapping);
            settings.multiOptionTypeMapping.TryCloneDeep(out this.multiOptionTypeMapping);
            genericTypeMapping = new (settings.genericTypeMapping);
            customTypeReaders = new (settings.customTypeReaders);
            customTypeNames = new (settings.customTypeNames);
            forbiddenTypes = new (settings.forbiddenTypes);

            enableReferenceResolution = settings.enableReferenceResolution;
            enableStringRefResolution = settings.enableStringRefResolution;
            proposedTypeHandling = settings.proposedTypeHandling;
            addCaseVariantsForCustomTypeNames = settings.addCaseVariantsForCustomTypeNames;

            initialBufferSize = settings.initialBufferSize.ClampLow(1024 * 16); // minimum 16KB buffer size to avoid too many resizes for larger JSON inputs
            tryCastArraysOfUnknownValues = settings.tryCastArraysOfUnknownValues;
            rethrowExceptions = settings.rethrowExceptions;
            logCatchedExceptions = settings.logCatchedExceptions;
            strict = settings.strict;
            populateExistingMembers = settings.populateExistingMembers;
            useStringCache = settings.useStringCache;
            stringCacheBitSize = settings.stringCacheBitSize;
            stringCacheMaxLength = settings.stringCacheMaxLength;
            allowUninitializedObjectCreation = settings.allowUninitializedObjectCreation;

            typeWhitelistMode = settings.typeWhitelistMode;
            allowedTypes = new (settings.allowedTypes);
            allowedNamespacePrefixes = new (settings.allowedNamespacePrefixes, StringComparer.Ordinal);
        }
    }

}
