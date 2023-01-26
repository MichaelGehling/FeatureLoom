using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace FeatureLoom.TCP
{
    public class JsonMessageStreamReader : IMessageStreamReader
    {
        JsonSerializer serializer = Serialization.Json.ComplexObjectsStructure_Serializer;
        StreamReader streamReader;
        JsonTextReader jsonReader;
        Stream cachedStream;

        public Task<object> ReadMessage(Stream stream, CancellationToken cancellationToken)
        {
            UpdateStreamReader(stream);
            object message = serializer.Deserialize(jsonReader);
            return Task.FromResult(message);
        }

        void UpdateStreamReader(Stream stream)
        {
            if (cachedStream == stream) return;
            Dispose();

            cachedStream = stream;
            streamReader = new StreamReader(stream);
            jsonReader = new JsonTextReader(streamReader);
        }

        public void Dispose()
        {
            streamReader?.Dispose();
            ((IDisposable)jsonReader)?.Dispose();
            cachedStream = null;
            streamReader = null;
            jsonReader = null;
        }
    }
}