using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System.IO;
using System.Text;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerApiSurfaceTests
    {
        [Fact]
        public void SetDataSource_String_Then_TryDeserialize_OutT_Works()
        {
            var deserializer = new FeatureJsonDeserializer();

            deserializer.SetDataSource("123");

            Assert.True(deserializer.TryDeserialize(out int value));
            Assert.Equal(123, value);
        }

        [Fact]
        public void SetDataSource_Stream_Then_TryDeserialize_OutT_Works()
        {
            var deserializer = new FeatureJsonDeserializer();
            using var stream = Utf8Stream("\"stream\"");

            deserializer.SetDataSource(stream);

            Assert.True(deserializer.TryDeserialize(out string value));
            Assert.Equal("stream", value);
        }

        [Fact]
        public void SetDataSource_ByteSegment_Then_TryDeserialize_OutT_Works()
        {
            var deserializer = new FeatureJsonDeserializer();
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes("\"abc\""), true);

            deserializer.SetDataSource(bytes);

            Assert.True(deserializer.TryDeserialize(out string value));
            Assert.Equal("abc", value);
        }

        [Fact]
        public void TryDeserialize_OutT_WhitespaceOnly_ReturnsFalse()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("   \t\r\n  ");

            Assert.False(deserializer.TryDeserialize(out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void IsAnyDataLeft_Tracks_SequentialValues()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("   1   2   ");

            Assert.True(deserializer.IsAnyDataLeft());

            Assert.True(deserializer.TryDeserialize(out int first));
            Assert.Equal(1, first);
            Assert.True(deserializer.IsAnyDataLeft());

            Assert.True(deserializer.TryDeserialize(out int second));
            Assert.Equal(2, second);

            Assert.False(deserializer.IsAnyDataLeft());
        }

        [Fact]
        public void SkipBufferUntil_FindsDelimiter_AndContinuesDeserialization()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("prefix::42");

            deserializer.SkipBufferUntil("::", alsoSkipDelimiter: true, out bool found);

            Assert.True(found);
            Assert.True(deserializer.TryDeserialize(out int value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void SkipBufferUntil_DelimiterNotFound_SetsFoundFalse()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("prefix-without-delimiter");

            deserializer.SkipBufferUntil("::", alsoSkipDelimiter: true, out bool found);

            Assert.False(found);
            Assert.False(deserializer.TryDeserialize(out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void SkipBufferUntil_AlsoSkipDelimiterFalse_LeavesDelimiterAtCurrentPosition()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("abc::123");

            deserializer.SkipBufferUntil("::", alsoSkipDelimiter: false, out bool found);

            Assert.True(found);
            Assert.False(deserializer.TryDeserialize(out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void TryDeserialize_TypeOverload_Works_ForPrimitive()
        {
            var deserializer = new FeatureJsonDeserializer();

            Assert.True(deserializer.TryDeserialize("7", typeof(int), out object value));

            Assert.IsType<int>(value);
            Assert.Equal(7, (int)value);
        }

        [Fact]
        public void TryDeserialize_TypeOverload_Works_WithSetDataSource()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("{\"A\":9}");

            Assert.True(deserializer.TryDeserialize(typeof(ComplexValue), out object value));

            var typed = Assert.IsType<ComplexValue>(value);
            Assert.Equal(9, typed.A);
        }

        [Fact]
        public void TryDeserialize_Stream_TypeOverload_Works_ForComplexType()
        {
            var deserializer = new FeatureJsonDeserializer();
            using var stream = Utf8Stream("{\"A\":9}");

            Assert.True(deserializer.TryDeserialize(stream, typeof(ComplexValue), out object value));

            var typed = Assert.IsType<ComplexValue>(value);
            Assert.Equal(9, typed.A);
        }

        [Fact]
        public void TryDeserialize_ByteSegment_TypeOverload_Works_ForComplexType()
        {
            var deserializer = new FeatureJsonDeserializer();
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes("{\"A\":11}"), true);

            Assert.True(deserializer.TryDeserialize(bytes, typeof(ComplexValue), out object value));

            var typed = Assert.IsType<ComplexValue>(value);
            Assert.Equal(11, typed.A);
        }

        [Fact]
        public void TryPopulate_Class_Overload_UpdatesExistingInstance()
        {
            var deserializer = new FeatureJsonDeserializer();
            var item = new ComplexValue { A = 1, B = "old" };

            Assert.True(deserializer.TryPopulate("{\"A\":5,\"B\":\"new\"}", item));

            Assert.Equal(5, item.A);
            Assert.Equal("new", item.B);
        }

        [Fact]
        public void TryPopulate_Class_StreamOverload_UpdatesExistingInstance()
        {
            var deserializer = new FeatureJsonDeserializer();
            var item = new ComplexValue { A = 1, B = "old" };
            using var stream = Utf8Stream("{\"A\":6,\"B\":\"stream\"}");

            Assert.True(deserializer.TryPopulate(stream, item));

            Assert.Equal(6, item.A);
            Assert.Equal("stream", item.B);
        }

        [Fact]
        public void TryPopulate_Class_ByteSegmentOverload_UpdatesExistingInstance()
        {
            var deserializer = new FeatureJsonDeserializer();
            var item = new ComplexValue { A = 1, B = "old" };
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes("{\"A\":7,\"B\":\"bytes\"}"), true);

            Assert.True(deserializer.TryPopulate(bytes, item));

            Assert.Equal(7, item.A);
            Assert.Equal("bytes", item.B);
        }

        [Fact]
        public void TryPopulate_Struct_Overload_UpdatesValueByRef()
        {
            var deserializer = new FeatureJsonDeserializer();
            var item = new ValueStruct { A = 1, B = 2 };

            Assert.True(deserializer.TryPopulate("{\"A\":11,\"B\":22}", ref item));

            Assert.Equal(11, item.A);
            Assert.Equal(22, item.B);
        }

        [Fact]
        public void TryPopulate_Struct_StreamOverload_UpdatesValueByRef()
        {
            var deserializer = new FeatureJsonDeserializer();
            var item = new ValueStruct { A = 1, B = 2 };
            using var stream = Utf8Stream("{\"A\":33,\"B\":44}");

            Assert.True(deserializer.TryPopulate(stream, ref item));

            Assert.Equal(33, item.A);
            Assert.Equal(44, item.B);
        }

        [Fact]
        public void TryPopulate_Struct_ByteSegmentOverload_UpdatesValueByRef()
        {
            var deserializer = new FeatureJsonDeserializer();
            var item = new ValueStruct { A = 1, B = 2 };
            var bytes = new ByteSegment(Encoding.UTF8.GetBytes("{\"A\":55,\"B\":66}"), true);

            Assert.True(deserializer.TryPopulate(bytes, ref item));

            Assert.Equal(55, item.A);
            Assert.Equal(66, item.B);
        }

        [Fact]
        public void IsAnyDataLeft_Stream_WhitespaceOnly_ReturnsFalse()
        {
            var deserializer = new FeatureJsonDeserializer();
            using var stream = Utf8Stream("   \t\r\n");

            deserializer.SetDataSource(stream);

            Assert.False(deserializer.IsAnyDataLeft());
            Assert.False(deserializer.TryDeserialize(out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void IsAnyDataLeft_Stream_WhitespaceThenValue_ReturnsTrue()
        {
            var deserializer = new FeatureJsonDeserializer();
            using var stream = Utf8Stream("   5");

            deserializer.SetDataSource(stream);

            Assert.True(deserializer.IsAnyDataLeft());
            Assert.True(deserializer.TryDeserialize(out int value));
            Assert.Equal(5, value);
        }

        [Fact]
        public void SkipBufferUntil_EmptyDelimiter_DoesNothing()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("123");

            deserializer.SkipBufferUntil(string.Empty, alsoSkipDelimiter: true, out bool found);

            Assert.False(found);
            Assert.True(deserializer.TryDeserialize(out int value));
            Assert.Equal(123, value);
        }

        [Fact]
        public void SkipBufferUntil_DelimiterAtEnd_AlsoSkipDelimiterTrue_LeavesNoFurtherData()
        {
            var deserializer = new FeatureJsonDeserializer();
            deserializer.SetDataSource("noise::");

            deserializer.SkipBufferUntil("::", alsoSkipDelimiter: true, out bool found);

            Assert.True(found);
            Assert.False(deserializer.TryDeserialize(out int value));
            Assert.Equal(default, value);
        }

        private static MemoryStream Utf8Stream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

        private class ComplexValue
        {
            public int A;
            public string B;
        }

        private struct ValueStruct
        {
            public int A;
            public int B;
        }
    }
}