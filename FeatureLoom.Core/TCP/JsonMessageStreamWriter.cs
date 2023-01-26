using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace FeatureLoom.TCP
{
    public class JsonMessageStreamWriter : IMessageStreamWriter
    {
        JsonSerializer serializer = Serialization.Json.ComplexObjectsStructure_Serializer;
        JsonTextWriter jsonWriter;
        StreamWriter streamWriter;
        Stream cachedStream;        

        public Task WriteMessage(object message, Stream stream, CancellationToken cancellationToken)
        {
            UpdateStreamWriter(stream);
            serializer.Serialize(jsonWriter, message);
            jsonWriter.Flush();
            return Task.CompletedTask;
        }

        void UpdateStreamWriter(Stream stream)
        {
            if (cachedStream == stream) return;
            Dispose();

            cachedStream = stream;
            streamWriter = new StreamWriter(stream);
            jsonWriter = new JsonTextWriter(streamWriter);
        }

        public void Dispose()
        {            
            streamWriter?.Dispose();
            ((IDisposable)jsonWriter)?.Dispose();
            cachedStream = null;
            streamWriter = null;
            jsonWriter = null;
        }
    }
}