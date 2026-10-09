using FeatureLoom.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonLinesTests
    {
        public class Record
        {
            public int id;
            public string name;
            public List<int> values;
        }

        private static JsonSerializer CreateJsonLinesSerializer() =>
            new JsonSerializer(new JsonSerializer.Settings { formatting = JsonSerializer.JsonFormatting.JsonLines });

        private static JsonDeserializer CreateJsonLinesDeserializer(int bufferSize = 0)
        {
            var settings = new JsonDeserializer.Settings
            {
                inputFormat = JsonDeserializer.Settings.InputFormat.JsonLines,
                logCatchedExceptions = false
            };
            if (bufferSize > 0) settings.initialBufferSize = bufferSize;
            return new JsonDeserializer(settings);
        }

        private static List<Record> ReadAll(JsonDeserializer deserializer, out int failures)
        {
            var result = new List<Record>();
            failures = 0;
            while (deserializer.IsAnyDataLeft())
            {
                if (deserializer.TryDeserialize(out Record record)) result.Add(record);
                else failures++;
                Assert.True(failures < 5000, "Deserialization does not make progress");
            }
            return result;
        }

        [Fact]
        public void Serialize_String_AppendsLineFeed()
        {
            var serializer = CreateJsonLinesSerializer();
            Assert.Equal("{\"id\":1,\"name\":\"a\",\"values\":[1,2]}\n", serializer.Serialize(new Record { id = 1, name = "a", values = new List<int> { 1, 2 } }));
            Assert.Equal("42\n", serializer.Serialize(42));
            Assert.Equal("null\n", serializer.Serialize<Record>(null));
        }

        [Fact]
        public void Serialize_Compact_HasNoLineFeed()
        {
            var serializer = new JsonSerializer();
            Assert.Equal("42", serializer.Serialize(42));
        }

        [Fact]
        public void Serialize_StringWithLineFeed_IsEscaped()
        {
            var serializer = CreateJsonLinesSerializer();
            Assert.Equal("\"a\\nb\"\n", serializer.Serialize("a\nb"));
        }

        [Fact]
        public async Task Serialize_MultipleValuesToStream_ProducesJsonLines()
        {
            var serializer = CreateJsonLinesSerializer();
            using var stream = new MemoryStream();
            serializer.Serialize(stream, 1);
            await serializer.SerializeAsync(stream, "x");
            serializer.Serialize<Record>(stream, null);
            await serializer.SerializeAsync<Record>(stream, null);

            Assert.Equal("1\n\"x\"\nnull\nnull\n", Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact]
        public void RoundTrip_ThroughStream()
        {
            var serializer = CreateJsonLinesSerializer();
            using var stream = new MemoryStream();
            for (int i = 0; i < 100; i++) serializer.Serialize(stream, new Record { id = i, name = "n" + i, values = new List<int> { i, i + 1 } });
            stream.Position = 0;

            var deserializer = CreateJsonLinesDeserializer();
            deserializer.SetDataSource(stream);
            var records = ReadAll(deserializer, out int failures);

            Assert.Equal(0, failures);
            Assert.Equal(100, records.Count);
            for (int i = 0; i < 100; i++) Assert.Equal(i, records[i].id);
        }

        [Fact]
        public void Deserialize_BrokenLines_AreSkipped()
        {
            string input =
                "{\"id\":1}\n" +
                "{\"id\":2,\"name\":\n" +           // truncated: parser continues into the next line
                "{\"id\":3}\n" +
                "{\"id\":x}\n" +                    // invalid value
                "{\"id\":4}\n" +
                "garbage garbage\n" +
                "{\"id\":5}";

            var deserializer = CreateJsonLinesDeserializer();
            deserializer.SetDataSource(input);
            var records = ReadAll(deserializer, out int failures);

            Assert.Equal(new[] { 1, 3, 4, 5 }, records.ConvertAll(r => r.id));
            Assert.Equal(3, failures);
        }

        [Fact]
        public void Deserialize_BrokenLastLineWithoutLineFeed_Terminates()
        {
            var deserializer = CreateJsonLinesDeserializer();
            deserializer.SetDataSource("{\"id\":1}\n{\"id\":");
            var records = ReadAll(deserializer, out int failures);

            Assert.Single(records);
            Assert.Equal(1, failures);
        }

        [Fact]
        public void Deserialize_TruncatedRecord_DoesNotSkipFollowingLine()
        {
            // The parser reads past the LF into the next line before failing.
            var deserializer = CreateJsonLinesDeserializer();
            deserializer.SetDataSource("{\"id\":1,\n{\"id\":2}\n{\"id\":3}\n");
            var records = ReadAll(deserializer, out int failures);

            Assert.Equal(new[] { 2, 3 }, records.ConvertAll(r => r.id));
            Assert.Equal(1, failures);
        }

        [Fact]
        public void Deserialize_BrokenLinesFromStreamWithSmallBuffer_AreSkipped()
        {
            var sb = new StringBuilder();
            var expected = new List<int>();
            for (int i = 0; i < 2000; i++)
            {
                if (i % 7 == 3) sb.Append("{\"id\":").Append(i).Append(",\"name\":\"").Append(new string('z', i % 50)).Append("\n");
                else
                {
                    sb.Append("{\"id\":").Append(i).Append(",\"name\":\"").Append(new string('z', i % 50)).Append("\"}\n");
                    expected.Add(i);
                }
            }
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

            var deserializer = CreateJsonLinesDeserializer(16 * 1024);
            deserializer.SetDataSource(stream);
            var records = ReadAll(deserializer, out int failures);

            Assert.Equal(expected, records.ConvertAll(r => r.id));
            Assert.Equal(2000 - expected.Count, failures);
        }
    }
}
