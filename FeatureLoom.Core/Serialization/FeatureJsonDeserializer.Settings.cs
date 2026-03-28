using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace FeatureLoom.Serialization
{
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
            public bool enableProposedTypes = false;

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

            public void AddAllowedType(Type type) => allowedTypes.Add(type);
            public void AddAllowedType<T>() => allowedTypes.Add(typeof(T));
            public void AddAllowedNamespacePrefix(string prefix)
            {
                if (!string.IsNullOrWhiteSpace(prefix)) allowedNamespacePrefixes.Add(prefix);
            }
        }
    }
}
