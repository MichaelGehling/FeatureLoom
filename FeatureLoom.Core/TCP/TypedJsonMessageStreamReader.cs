using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using FeatureLoom.TCP;
using FeatureLoom.Extensions;

namespace FeatureLoom.TCP
{
    public class TypedJsonMessageStreamReader : ISpecificMessageStreamReader
    {
        JsonSerializer serializer = Serialization.Json.ComplexObjectsStructure_Serializer;
        StreamReader streamReader;
        JsonTextReader jsonReader;
        byte[] typeInfo = "TypedJSON".ToByteArray();

        public Task<object> ReadMessage(Stream stream, int messageLength, CancellationToken cancellationToken)
        {
            UpdateStreamReader(stream);
            object message = serializer.Deserialize(jsonReader);
            return Task.FromResult(message);
        }

        void UpdateStreamReader(Stream stream)
        {
            if (streamReader?.BaseStream != stream) streamReader = new StreamReader(stream);
            jsonReader = new JsonTextReader(streamReader);
        }

        public void Dispose()
        {
            streamReader?.Dispose();
            ((IDisposable)jsonReader)?.Dispose();
            streamReader = null;
            jsonReader = null;
        }

        public bool CanRead(byte[] typeInfoBuffer, int typeInfoStartIndex, int typeInfoLength)
        {
            return typeInfoBuffer.CompareTo(typeInfo, typeInfoStartIndex, 0, typeInfoLength);
        }

        
    }
}