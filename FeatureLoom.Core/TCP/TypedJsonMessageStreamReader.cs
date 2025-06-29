using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using FeatureLoom.Serialization;
using FeatureLoom.TCP;
using FeatureLoom.Extensions;

namespace FeatureLoom.TCP
{
    public class TypedJsonMessageStreamReader : ISpecificMessageStreamReader
    {
        FeatureJsonDeserializer deserializer = new FeatureJsonDeserializer(new()
        {
            enableProposedTypes = true,
            enableReferenceResolution = true
        });
        byte[] typeInfo = "TypedJSON".ToByteArray();

        public TypedJsonMessageStreamReader(FeatureJsonDeserializer deserializer)
        {
            this.deserializer = deserializer;
        }

        public TypedJsonMessageStreamReader()
        {
        }

        public Task<object> ReadMessage(Stream stream, int messageLength, CancellationToken cancellationToken)
        {
            if (!deserializer.TryDeserialize(stream, out object message)) throw new Exception("Failed deserializing from stream");
            return Task.FromResult(message);
        }

        public void Dispose()
        {

        }

        public bool CanRead(byte[] typeInfoBuffer, int typeInfoStartIndex, int typeInfoLength)
        {
            return typeInfoBuffer.CompareTo(typeInfo, typeInfoStartIndex, 0, typeInfoLength);
        }

        
    }
}