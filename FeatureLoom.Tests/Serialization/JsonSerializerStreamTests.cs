using FeatureLoom.Serialization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerStreamTests
    {
        private static string ReadStreamString(MemoryStream stream)
        {
            stream.Position = 0;
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        [Fact]
        public void Serialize_Stream_Sync_FlushesAllData()
        {
            string payload = new string('a', 200);
            var settings = new JsonSerializer.Settings { writeBufferChunkSize = 32 };
            var serializer = new JsonSerializer(settings);

            using var stream = new MemoryStream();
            serializer.Serialize(stream, payload);

            string json = ReadStreamString(stream);
            string expected = $"\"{payload}\"";

            Assert.Equal(expected, json);
            Assert.True(stream.Length > settings.writeBufferChunkSize);
        }

        [Fact]
        public async Task Serialize_Stream_Async_FlushesAllData()
        {
            string payload = new string('b', 200);
            var settings = new JsonSerializer.Settings { writeBufferChunkSize = 32 };
            var serializer = new JsonSerializer(settings);

            using var stream = new MemoryStream();
            await serializer.SerializeAsync(stream, payload);

            string json = ReadStreamString(stream);
            string expected = $"\"{payload}\"";

            Assert.Equal(expected, json);
            Assert.True(stream.Length > settings.writeBufferChunkSize);
        }
    }
}