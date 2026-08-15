using System;
using System.IO;
using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Serialization
{
    // Profiling probe (not a correctness test): exists so the array read path can be measured with
    // CPU *instrumentation*, which counts real call boundaries instead of relying on stack walking.
    // Sampling traces of the equivalent BenchmarkDotNet run produced impossible caller/callee edges
    // (e.g. Pool<T> -> Engine.Measure), so per-element costs could not be attributed there.
    public class JsonDeserializerArrayProfilingProbe
    {
        private const int ArraySize = 1000;
        private const int Iterations = 200;

        [Fact]
        public void Profile_DateTimeArray_ReadPath()
        {
            var value = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var array = new DateTime[ArraySize];
            for (int i = 0; i < array.Length; i++) array[i] = value;

            var serializer = new JsonSerializer();
            var stream = new MemoryStream();
            serializer.Serialize(stream, array);

            var deserializer = new JsonDeserializer();
            for (int i = 0; i < Iterations; i++)
            {
                stream.Position = 0;
                Assert.True(deserializer.TryDeserialize(stream, out DateTime[] result));
                Assert.Equal(ArraySize, result.Length);
            }
        }
    }
}
