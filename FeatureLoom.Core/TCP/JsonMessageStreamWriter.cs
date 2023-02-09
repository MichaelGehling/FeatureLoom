using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using FeatureLoom.Core.TCP;
using FeatureLoom.Extensions;

namespace FeatureLoom.TCP
{
    public class JsonMessageStreamWriter : IGeneralMessageStreamWriter, ISpecificMessageStreamWriter
    {
        JsonSerializer serializer = Serialization.Json.ComplexObjectsStructure_Serializer;
        JsonTextWriter jsonWriter;
        StreamWriter streamWriter;
        //byte[] typeInfo = "TypedJSON".ToByteArray();
        byte[] typeInfo = "".ToByteArray();

        public Task WriteMessage<T>(T message, Stream stream, CancellationToken cancellationToken)
        {
            UpdateStreamWriter(stream);
            serializer.Serialize(jsonWriter, message);
            jsonWriter.Flush();
            return Task.CompletedTask;
        }

        void UpdateStreamWriter(Stream stream)
        {
            streamWriter = new StreamWriter(stream);
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