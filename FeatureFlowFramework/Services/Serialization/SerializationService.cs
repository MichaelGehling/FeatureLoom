using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FeatureFlowFramework.Services.Serialization
{
    public static class SerializationService
    {
        class ContextData : IServiceContextData
        {
            public Dictionary<int, ISerializer> idToSerializer;
            public Dictionary<Type, ISerializer> typeToSerializer;
            public FeatureLock myLock = new FeatureLock();
            public ISerializer defaultSerializer;

            public ContextData()
            {
                idToSerializer = new Dictionary<int, ISerializer>();
                typeToSerializer = new Dictionary<Type, ISerializer>();                
            }

            public ContextData(Dictionary<int, ISerializer> idToSerializer, Dictionary<Type, ISerializer> typeToSerializer)
            {
                this.idToSerializer = new Dictionary<int, ISerializer>(idToSerializer);
                this.typeToSerializer = new Dictionary<Type, ISerializer>(typeToSerializer);
            }

            public IServiceContextData Copy()
            {
                
                using (myLock.ForReading())
                {
                    var newContext = new ContextData(idToSerializer, typeToSerializer);
                    newContext.defaultSerializer = defaultSerializer;
                    return newContext;
                }                
            }
        }
        static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        static SerializationService()
        {

        }

        public static void AddSerializer(ISerializer serializer, bool setAsDefault = false, bool failIfIdExists = true)
        {
            using (context.Data.myLock.ForWriting())
            {
                if (context.Data.idToSerializer.ContainsKey(serializer.SerializerId))
                {
                    if (failIfIdExists) throw new Exception($"Serializer with Id {serializer.SerializerId} already exists!");
                    foreach (var pair in context.Data.typeToSerializer.Where(pair => pair.Value.SerializerId == serializer.SerializerId))
                    {
                        context.Data.typeToSerializer[pair.Key] = serializer;
                    }
                }
                context.Data.idToSerializer[serializer.SerializerId] = serializer;
                if (setAsDefault) context.Data.defaultSerializer = serializer;
            }
        }

        public static ISerializer GetSerializer<T>()
        {
            using (context.Data.myLock.ForReading())
            {
                if (context.Data.typeToSerializer.TryGetValue(typeof(T), out var serializer)) return serializer;
                else return context.Data.defaultSerializer;
            }
        }
        

        public static bool TryGetSerializerById(int id, out ISerializer serializer)
        {
            using (context.Data.myLock.ForReading())
            {
                return context.Data.idToSerializer.TryGetValue(id, out serializer);
            }
        }

        public static bool TrySerialize<T>(this T obj, out ISerializedObject serializedObject)
        {
            var serializer = GetSerializer<T>();
            if (serializer == null)
            {
                serializedObject = null;
                return false;
            }
            else
            {
                return serializer.TrySerialize(obj, out serializedObject);
            }
        }        
    }
}
