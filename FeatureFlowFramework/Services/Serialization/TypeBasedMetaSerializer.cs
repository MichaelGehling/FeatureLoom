using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Services.MetaData;

namespace FeatureFlowFramework.Services.Serialization
{
    public class TypeBasedMetaSerializer : ISerializer
    {
        Dictionary<Type, ISerializer> typeToSerializer = new Dictionary<Type, ISerializer>();        
        FeatureLock myLock = new FeatureLock();
        ISerializer defaultSerializer;

        public TypeBasedMetaSerializer(ISerializer defaultSerializer)
        {
            this.defaultSerializer = defaultSerializer ?? throw new Exception("DefaultSerializer may not be null!");
        }

        public void AddSerializer(ISerializer serializer, Type type)
        {
            using(myLock.ForWriting())
            {
                typeToSerializer.Add(type, serializer);
            }
        }

        private ISerializer GetSerializerByType(Type type)
        {
            ISerializer serializer;
            using(myLock.ForReading())
            {
                if(!typeToSerializer.TryGetValue(type, out serializer))
                {
                    serializer = defaultSerializer;
                }
            }
            return serializer;
        }

        public bool TryDeserialize<T>(byte[] data, out T obj)
        {
            var serializer = GetSerializerByType(typeof(T));
            return serializer.TryDeserialize<T>(data, out obj);
        }

        public Task<AsyncOut<bool, T>> TryDeserializeFromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            var serializer = GetSerializerByType(typeof(T));
            return serializer.TryDeserializeFromStreamAsync<T>(stream, cancellationToken);
        }

        public Task<AsyncOut<bool, ISerializedObject>> TryReadSerializedObjectFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Log.WARNING(this.GetHandle(), "TypeBasedMetaSerializer cannot identify correct serializer without the target type!");
            return Task.FromResult(new AsyncOut<bool, ISerializedObject>(false, null));
        }

        public bool TrySerialize<T>(T obj, out ISerializedObject serializedObject)
        {
            var serializer = GetSerializerByType(typeof(T));
            return serializer.TrySerialize(obj, out serializedObject);
        }

        public Task<bool> TrySerializeToStreamAsync<T>(T obj, Stream stream, CancellationToken cancellationToken = default)
        {
            var serializer = GetSerializerByType(typeof(T));
            return serializer.TrySerializeToStreamAsync(obj, stream, cancellationToken);
        }
    }
}
