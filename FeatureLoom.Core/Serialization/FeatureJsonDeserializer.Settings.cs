using System;
using System.Collections.Generic;
using FeatureLoom.Extensions;
using System.Collections;
using FeatureLoom.Helpers;
using System.Collections.Concurrent;

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
            public bool enableReferenceResolution = false;
            public bool enableProposedTypes = false;
            public int initialBufferSize = 1024 * 64;
            public bool tryCastArraysOfUnknownValues = true;
            public bool rethrowExceptions = true;
            public bool logCatchedExceptions = true;
            public bool strict = false;
            public bool populateExistingMembers = false;

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
            }

            public void AddConstructor<T>(Func<T> constructor) => constructors[typeof(T)] = constructor;
            public void AddConstructorWithParameter<T, P>(Func<P, T> constructor) => constructorsWithParam[(typeof(T), typeof(P))] = constructor;
            public void AddTypeMapping(Type baseType, Type mappedType)
            {
                if (!mappedType.IsAssignableTo(baseType)) throw new Exception($"{TypeNameHelper.GetSimplifiedTypeName(baseType)} is not implemented by {TypeNameHelper.GetSimplifiedTypeName(mappedType)}");
                typeMapping[baseType] = mappedType;
            }
            public void AddGenericTypeMapping(Type genericBaseType, Type genericImplType)
            {
                if (!genericImplType.IsOfGenericType(genericBaseType)) throw new Exception($"{TypeNameHelper.GetSimplifiedTypeName(genericBaseType)} is not implemented by {TypeNameHelper.GetSimplifiedTypeName(genericImplType)}");
                genericTypeMapping[genericBaseType] = genericImplType;
            }

            public void AddMultiOptionTypeMapping(Type baseType, params Type[] typeOptions)
            {
                foreach(var typeOption in typeOptions)
                {
                    if (!typeOption.IsAssignableTo(baseType)) throw new Exception($"{TypeNameHelper.GetSimplifiedTypeName(baseType)} is not implemented by {TypeNameHelper.GetSimplifiedTypeName(typeOption)}");
                }
                multiOptionTypeMapping[baseType] = typeOptions;
            }

            public void AddCustomTypeReader<T>(ICustomTypeReader<T> customTypeReader)
            {
                customTypeReaders[typeof(T)] = customTypeReader;
            }

            public void AddCustomTypeReader<T>(JsonDataTypeCategory category, Func<ExtensionApi, T> readType)
            {
                AddCustomTypeReader<T>(new CustomTypeReader<T>(category, readType));
            }

        }
    }
}
