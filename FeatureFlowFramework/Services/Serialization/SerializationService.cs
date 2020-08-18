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
        static SerializationService()
        {
            TypeBasedMetaSerializer standardSerializer = new TypeBasedMetaSerializer(new UnivarsalJsonSerializer());            
            AddSerializer(standardSerializer, "default", true, false);
        }

        class ContextData : IServiceContextData
        {
            public Dictionary<string, ISerializer> serializers;
            public FeatureLock serializersLock = new FeatureLock();
            public ISerializer defaultSerializer;

            public ContextData()
            {
                serializers = new Dictionary<string, ISerializer>();               
            }

            public ContextData(Dictionary<string, ISerializer> serializers)
            {
                this.serializers = new Dictionary<string, ISerializer>(serializers);
            }

            public IServiceContextData Copy()
            {
                using (serializersLock.ForReading())
                {
                    var newContext = new ContextData(serializers);
                    newContext.defaultSerializer = defaultSerializer;
                    return newContext;
                }                
            }
        }
        static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static void AddSerializer(ISerializer serializer, string id, bool setAsDefault = false, bool failIfIdExists = true)
        {
            using (context.Data.serializersLock.ForWriting())
            {
                if (failIfIdExists && context.Data.serializers.ContainsKey(id)) throw new Exception($"Serializer with Id {id} already exists!");

                context.Data.serializers[id] = serializer;
                if (setAsDefault) context.Data.defaultSerializer = serializer;
            }
        }

        public static ISerializer DefaultSerializer => context.Data.defaultSerializer;
        

        public static bool TryGetSerializerById(string id, out ISerializer serializer)
        {
            using (context.Data.serializersLock.ForReading())
            {
                return context.Data.serializers.TryGetValue(id, out serializer);
            }
        }      
    }
}
