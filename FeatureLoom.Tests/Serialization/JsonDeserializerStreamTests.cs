using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System.IO;
using System.Text;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerStreamTests
    {
        private static MemoryStream Utf8Stream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

        private class StreamSample
        {
            public int A;
            public string B;
        }

        private struct StreamStructSample
        {
            public int A;
            public int B;
        }

        [Fact]
        public void Deserialize_Stream_SameDeserializer_ReadsMultipleValues()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("1 2 3");

            Assert.True(deserializer.TryDeserialize(stream, out int first));
            Assert.True(deserializer.TryDeserialize(stream, out int second));
            Assert.True(deserializer.TryDeserialize(stream, out int third));
            Assert.False(deserializer.TryDeserialize(stream, out int noMore));

            Assert.Equal(1, first);
            Assert.Equal(2, second);
            Assert.Equal(3, third);
        }

        [Fact]
        public void Deserialize_Stream_SameDeserializer_ReadsMultipleObjects_WithTypeOverload()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("{\"A\":1,\"B\":\"x\"}\n{\"A\":2,\"B\":\"y\"}");

            Assert.True(deserializer.TryDeserialize(stream, typeof(StreamSample), out object firstObj));
            Assert.True(deserializer.TryDeserialize(stream, typeof(StreamSample), out object secondObj));
            Assert.False(deserializer.TryDeserialize(stream, typeof(StreamSample), out object noMore));

            var first = Assert.IsType<StreamSample>(firstObj);
            var second = Assert.IsType<StreamSample>(secondObj);

            Assert.Equal(1, first.A);
            Assert.Equal("x", first.B);
            Assert.Equal(2, second.A);
            Assert.Equal("y", second.B);
        }

        [Fact]
        public void Deserialize_Stream_SameDeserializer_CanSwitchStreams()
        {
            var deserializer = new JsonDeserializer();

            using var stream1 = Utf8Stream("10");
            using var stream2 = Utf8Stream("20");

            Assert.True(deserializer.TryDeserialize(stream1, out int first));
            Assert.True(deserializer.TryDeserialize(stream2, out int second));

            stream1.Position = 0;
            Assert.True(deserializer.TryDeserialize(stream1, out int firstAgain));

            Assert.Equal(10, first);
            Assert.Equal(20, second);
            Assert.Equal(10, firstAgain);
        }

        [Fact]
        public void Deserialize_Stream_SameDeserializer_CanResetStream()
        {
            var deserializer = new JsonDeserializer();

            using var stream = Utf8Stream("10");

            Assert.True(deserializer.TryDeserialize(stream, out int first));

            stream.Position = 0;
            Assert.True(deserializer.TryDeserialize(stream, out int firstAgain));

            Assert.Equal(10, first);
            Assert.Equal(10, firstAgain);
        }

        [Fact]
        public void Populate_Stream_SameDeserializer_ReadsMultipleObjectsSequentially()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);
            using var stream = Utf8Stream("{\"A\":1}\n{\"B\":\"changed\"}");

            var item = new StreamSample
            {
                A = 0,
                B = "initial"
            };

            Assert.True(deserializer.TryPopulate(stream, item));
            Assert.Equal(1, item.A);
            Assert.Equal("initial", item.B);

            Assert.True(deserializer.TryPopulate(stream, item));
            Assert.Equal(1, item.A);
            Assert.Equal("changed", item.B);

            Assert.False(deserializer.TryPopulate(stream, item));
        }

        [Fact]
        public void Deserialize_ByteSegment_And_Type_Overloads_WithSameDeserializer()
        {
            var deserializer = new JsonDeserializer();

            var bytes1 = new ByteSegment(Encoding.UTF8.GetBytes("123"));
            var bytes2 = new ByteSegment(Encoding.UTF8.GetBytes("456"));

            Assert.True(deserializer.TryDeserialize(bytes1, out int first));
            Assert.True(deserializer.TryDeserialize(bytes2, typeof(int), out object secondObj));

            Assert.Equal(123, first);
            Assert.Equal(456, Assert.IsType<int>(secondObj));
        }

        [Fact]
        public void Deserialize_Stream_ThenParameterlessGenericOverload_ContinuesReading()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("7 8");

            Assert.True(deserializer.TryDeserialize(stream, out int first));
            Assert.True(deserializer.TryDeserialize(out int second));
            Assert.False(deserializer.TryDeserialize(out int noMore));

            Assert.Equal(7, first);
            Assert.Equal(8, second);
        }

        [Fact]
        public void Deserialize_Stream_ThenParameterlessTypeOverload_ContinuesReading()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("9 10");

            Assert.True(deserializer.TryDeserialize(stream, typeof(int), out object firstObj));
            Assert.True(deserializer.TryDeserialize(typeof(int), out object secondObj));
            Assert.False(deserializer.TryDeserialize(typeof(int), out object noMore));

            Assert.Equal(9, Assert.IsType<int>(firstObj));
            Assert.Equal(10, Assert.IsType<int>(secondObj));
            Assert.Null(noMore);
        }

        [Fact]
        public void Deserialize_String_Overload_ReusesSameDeserializer()
        {
            var deserializer = new JsonDeserializer();

            Assert.True(deserializer.TryDeserialize("100", out int first));
            Assert.True(deserializer.TryDeserialize("200", out int second));

            Assert.Equal(100, first);
            Assert.Equal(200, second);
        }

        [Fact]
        public void Populate_String_RefStruct_Overload_ReusesSameDeserializer()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);

            var item = new StreamStructSample
            {
                A = 1,
                B = 2
            };

            Assert.True(deserializer.TryPopulate("{\"A\":11}", ref item));
            Assert.True(deserializer.TryPopulate("{\"B\":22}", ref item));

            Assert.Equal(11, item.A);
            Assert.Equal(22, item.B);
        }

        [Fact]
        public void Populate_ByteSegment_Class_Overload_ReusesSameDeserializer()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);

            var item = new StreamSample
            {
                A = 0,
                B = "initial"
            };

            var bytes1 = new ByteSegment(Encoding.UTF8.GetBytes("{\"A\":5}"));
            var bytes2 = new ByteSegment(Encoding.UTF8.GetBytes("{\"B\":\"updated\"}"));

            Assert.True(deserializer.TryPopulate(bytes1, item));
            Assert.True(deserializer.TryPopulate(bytes2, item));

            Assert.Equal(5, item.A);
            Assert.Equal("updated", item.B);
        }

        [Fact]
        public void Populate_Stream_RefStruct_Overload_ReadsMultipleObjectsSequentially()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);
            using var stream = Utf8Stream("{\"A\":31}\n{\"B\":42}");

            var item = new StreamStructSample
            {
                A = 1,
                B = 2
            };

            Assert.True(deserializer.TryPopulate(stream, ref item));
            Assert.Equal(31, item.A);
            Assert.Equal(2, item.B);

            Assert.True(deserializer.TryPopulate(stream, ref item));
            Assert.Equal(31, item.A);
            Assert.Equal(42, item.B);

            Assert.False(deserializer.TryPopulate(stream, ref item));
        }

        [Fact]
        public void Populate_ByteSegment_RefStruct_Overload_ReusesSameDeserializer()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);

            var item = new StreamStructSample
            {
                A = 1,
                B = 2
            };

            var bytes1 = new ByteSegment(Encoding.UTF8.GetBytes("{\"A\":71}"));
            var bytes2 = new ByteSegment(Encoding.UTF8.GetBytes("{\"B\":72}"));

            Assert.True(deserializer.TryPopulate(bytes1, ref item));
            Assert.True(deserializer.TryPopulate(bytes2, ref item));

            Assert.Equal(71, item.A);
            Assert.Equal(72, item.B);
        }

        [Fact]
        public void Populate_Stream_Class_Overload_CanSwitchStreams()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);

            var item = new StreamSample
            {
                A = 0,
                B = "initial"
            };

            using var stream1 = Utf8Stream("{\"A\":111}");
            using var stream2 = Utf8Stream("{\"B\":\"switched\"}");

            Assert.True(deserializer.TryPopulate(stream1, item));
            Assert.True(deserializer.TryPopulate(stream2, item));

            Assert.Equal(111, item.A);
            Assert.Equal("switched", item.B);
        }

        [Fact]
        public void Deserialize_TypeOverload_WithStringInput_ReusesSameDeserializer()
        {
            var deserializer = new JsonDeserializer();

            Assert.True(deserializer.TryDeserialize("321", typeof(int), out object firstObj));
            Assert.True(deserializer.TryDeserialize("654", typeof(int), out object secondObj));

            Assert.Equal(321, Assert.IsType<int>(firstObj));
            Assert.Equal(654, Assert.IsType<int>(secondObj));
        }

        [Fact]
        public void SkipBufferUntil_FindsDelimiter_AndSkipsDelimiter()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("noise##123");

            deserializer.SetDataSource(stream);
            deserializer.SkipBufferUntil("##", alsoSkipDelimiter: true, out bool found);

            Assert.True(found);
            Assert.True(deserializer.TryDeserialize(out int value));
            Assert.Equal(123, value);
        }

        [Fact]
        public void SkipBufferUntil_FindsDelimiter_WithoutSkippingDelimiter_CanBeConsumedBySecondCall()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("noise##456");

            deserializer.SetDataSource(stream);

            deserializer.SkipBufferUntil("##", alsoSkipDelimiter: false, out bool foundFirst);
            Assert.True(foundFirst);

            // Cursor should still be at delimiter; consume it now.
            deserializer.SkipBufferUntil("##", alsoSkipDelimiter: true, out bool foundSecond);
            Assert.True(foundSecond);

            Assert.True(deserializer.TryDeserialize(out int value));
            Assert.Equal(456, value);
        }

        [Fact]
        public void SkipBufferUntil_DelimiterNotFound_ReturnsFalse_AndNoFurtherData()
        {
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings()
            {
                rethrowExceptions = false,
            });
            using var stream = Utf8Stream("noise without delimiter");

            deserializer.SetDataSource(stream);
            deserializer.SkipBufferUntil("##", alsoSkipDelimiter: true, out bool found);

            Assert.False(found);
            Assert.False(deserializer.TryDeserialize(out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void SkipBufferUntil_FindsDelimiter_WhenSplitAcrossBufferChunks()
        {
            var settings = new JsonDeserializer.Settings
            {
                initialBufferSize = 7
            };
            var deserializer = new JsonDeserializer(settings);
            using var stream = Utf8Stream("xxxxABCDEF789");

            deserializer.SetDataSource(stream);
            deserializer.SkipBufferUntil("ABCDEF", alsoSkipDelimiter: true, out bool found);

            Assert.True(found);
            Assert.True(deserializer.TryDeserialize(out int value));
            Assert.Equal(789, value);
        }

        [Fact]
        public void Deserialize_Stream_WhitespaceOnly_ReturnsFalse()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("   \t\r\n  ");

            Assert.False(deserializer.TryDeserialize(stream, out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Deserialize_Stream_TypeOverload_WhitespaceOnly_ReturnsFalse()
        {
            var deserializer = new JsonDeserializer();
            using var stream = Utf8Stream("   \t\r\n  ");

            Assert.False(deserializer.TryDeserialize(stream, typeof(int), out object value));
            Assert.Null(value);
        }

        [Fact]
        public void Populate_Stream_Class_WhitespaceOnly_ReturnsFalse_AndKeepsValues()
        {
            var settings = new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            };
            var deserializer = new JsonDeserializer(settings);
            using var stream = Utf8Stream("   \t\r\n  ");

            var item = new StreamSample
            {
                A = 7,
                B = "keep"
            };

            Assert.False(deserializer.TryPopulate(stream, item));
            Assert.Equal(7, item.A);
            Assert.Equal("keep", item.B);
        }

        [Fact]
        public void Deserialize_Stream_InvalidJson_ReturnsFalse_WhenRethrowDisabled()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);
            using var stream = Utf8Stream("{");

            Assert.False(deserializer.TryDeserialize(stream, out int value));
            Assert.Equal(default, value);
        }
    }
}