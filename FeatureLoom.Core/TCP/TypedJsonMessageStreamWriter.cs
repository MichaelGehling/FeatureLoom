using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using FeatureLoom.Serialization;
using FeatureLoom.Extensions;

namespace FeatureLoom.TCP
{
    public class TypedJsonMessageStreamWriter : ISpecificMessageStreamWriter
    {
        FeatureJsonSerializer serializer = new FeatureJsonSerializer(new()
        {
            enumAsString = true,
            indent = true,
        });
        byte[] typeInfo = "TypedJSON".ToByteArray();

        public TypedJsonMessageStreamWriter(FeatureJsonSerializer serializer)
        {
            this.serializer = serializer;
        }

        public TypedJsonMessageStreamWriter()
        {
        }

        public async Task WriteMessage<T>(T message, Stream stream, CancellationToken cancellationToken)
        {
            serializer.Serialize(stream, (object)message); // cast to object will force the serializer to add type information
            await stream.FlushAsync();
        }


        public void Dispose()
        {            
            serializer = null;
        }

        public bool CanWrite<T>(T message, out byte[] typeInfo)
        {
            typeInfo = this.typeInfo;
            return true;
        }
    }
}