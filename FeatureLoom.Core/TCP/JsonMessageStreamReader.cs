using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using FeatureLoom.Core.TCP;
using FeatureLoom.Extensions;

namespace FeatureLoom.TCP
{
    public class JsonMessageStreamReader : IGeneralMessageStreamReader, ISpecificMessageStreamReader
    {
        JsonSerializer serializer = Serialization.Json.ComplexObjectsStructure_Serializer;
        StreamReader streamReader;
        JsonTextReader jsonReader;
        //byte[] typeInfo = "TypedJSON".ToByteArray();
        byte[] typeInfo = "".ToByteArray();

        public Task<object> ReadMessage(Stream stream, CancellationToken cancellationToken)
        {
            UpdateStreamReader(stream);
            object message = serializer.Deserialize(jsonReader);
            return Task.FromResult(message);
        }

        void UpdateStreamReader(Stream stream)
        {
            //if (streamReader?.BaseStream != stream) streamReader = new StreamReader(stream);
            streamReader = new StreamReader(stream);
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
            if (typeInfoLength != typeInfo.Length) return false;

            int index = typeInfoStartIndex;
            foreach(byte b in typeInfo)
            {
                if (typeInfoBuffer[index++] != b) return false;
            }
            return true;
        }

        public Task<object> ReadMessage(Stream stream, int messageLength, CancellationToken cancellationToken)
        {        
            return ReadMessage(stream, cancellationToken);
        }
    }
}