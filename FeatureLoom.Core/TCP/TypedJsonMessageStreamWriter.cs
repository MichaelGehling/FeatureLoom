using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using FeatureLoom.Extensions;
using Newtonsoft.Json.Bson;
using FeatureLoom.Time;

namespace FeatureLoom.TCP
{
    public class TypedJsonMessageStreamWriter : ISpecificMessageStreamWriter
    {
        JsonSerializer serializer = Serialization.Json.ComplexObjectsStructure_Serializer;
        JsonTextWriter jsonWriter;
        StreamWriter streamWriter;
        byte[] typeInfo = "TypedJSON".ToByteArray();

        public async Task WriteMessage<T>(T message, Stream stream, CancellationToken cancellationToken)
        {            
            UpdateStreamWriter(stream);                        
            serializer.Serialize(jsonWriter, message);
            await jsonWriter.FlushAsync();            
        }

        void UpdateStreamWriter(Stream stream)
        {
            if (streamWriter?.BaseStream != stream) streamWriter = new StreamWriter(stream);
            jsonWriter = new JsonTextWriter(streamWriter);
        }

        public void Dispose()
        {            
            streamWriter?.Dispose();
            ((IDisposable)jsonWriter)?.Dispose();
            streamWriter = null;
            jsonWriter = null;
        }

        public bool CanWrite<T>(T message, out byte[] typeInfo)
        {
            typeInfo = this.typeInfo;
            return true;
        }
    }
}