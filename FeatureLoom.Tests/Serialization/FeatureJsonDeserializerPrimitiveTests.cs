using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System;
using System.Globalization;
using Xunit;
using static FeatureLoom.Serialization.FeatureJsonSerializerPrimitiveTests;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerPrimitiveTests
    {
        private static void AssertDeserialized<T>(string json, T expected)
        {
            var deserializer = new FeatureJsonDeserializer();
            Assert.True(deserializer.TryDeserialize(json, out T value));
            Assert.Equal(expected, value);
        }

        private static void AssertDeserializedNaN(string json)
        {
            var deserializer = new FeatureJsonDeserializer();
            Assert.True(deserializer.TryDeserialize(json, out double value));
            Assert.True(double.IsNaN(value));
        }

        private static void AssertDeserializedFloatNaN(string json)
        {
            var deserializer = new FeatureJsonDeserializer();
            Assert.True(deserializer.TryDeserialize(json, out float value));
            Assert.True(float.IsNaN(value));
        }

        [Fact]
        public void Deserialize_NullReference_ReturnsNull()
        {
            AssertDeserialized<string>("null", null);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void Deserialize_Bool(string json, bool expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void Deserialize_NullableBool(string json, bool? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("-128", (sbyte)-128)]
        [InlineData("127", (sbyte)127)]
        public void Deserialize_Sbyte(string json, sbyte expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("0", (byte)0)]
        [InlineData("255", (byte)255)]
        public void Deserialize_Byte(string json, byte expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("-32768", (short)-32768)]
        [InlineData("32767", (short)32767)]
        public void Deserialize_Short(string json, short expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("0", (ushort)0)]
        [InlineData("65535", (ushort)65535)]
        public void Deserialize_Ushort(string json, ushort expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("-2147483648", int.MinValue)]
        [InlineData("2147483647", int.MaxValue)]
        public void Deserialize_Int(string json, int expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("-2147483648", int.MinValue)]
        [InlineData("2147483647", int.MaxValue)]
        public void Deserialize_NullableInt(string json, int? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("0", 0u)]
        [InlineData("4294967295", uint.MaxValue)]
        public void Deserialize_Uint(string json, uint expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("-9223372036854775808", long.MinValue)]
        [InlineData("9223372036854775807", long.MaxValue)]
        public void Deserialize_Long(string json, long expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("0", 0ul)]
        [InlineData("18446744073709551615", ulong.MaxValue)]
        public void Deserialize_Ulong(string json, ulong expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("0", 0f)]
        [InlineData("1.5", 1.5f)]
        public void Deserialize_Float(string json, float expected)
        {
            AssertDeserialized(json, expected);
        }

        [Fact]
        public void Deserialize_Float_SpecialCases()
        {
            AssertDeserializedFloatNaN("\"NaN\"");
            AssertDeserialized("\"Infinity\"", float.PositiveInfinity);
            AssertDeserialized("\"-Infinity\"", float.NegativeInfinity);
        }

        [Theory]
        [InlineData("0", 0d)]
        [InlineData("1.5", 1.5d)]
        [InlineData("1.0123456789", 1.0123456789d)]
        [InlineData("1E20", 1e20d)]
        [InlineData("1E-6", 1e-6d)]
        public void Deserialize_Double(string json, double expected)
        {
            AssertDeserialized(json, expected);
        }

        [Fact]
        public void Deserialize_Double_SpecialCases()
        {
            AssertDeserializedNaN("\"NaN\"");
            AssertDeserialized("\"Infinity\"", double.PositiveInfinity);
            AssertDeserialized("\"-Infinity\"", double.NegativeInfinity);
        }

        [Theory]
        [InlineData("0", 0.0)]
        [InlineData("1.25", 1.25)]
        public void Deserialize_Decimal_AsDouble(string json, decimal expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("\"A\"", 'A')]
        [InlineData("\"\\n\"", '\n')]
        public void Deserialize_Char(string json, char expected)
        {
            AssertDeserialized(json, expected);
        }

        [Fact]
        public void Deserialize_String_EscapesAndUnicode()
        {
            AssertDeserialized("\"Line1\\nLine2\\t\\\"quote\\\"\\\\backslash\"", "Line1\nLine2\t\"quote\"\\backslash");
            AssertDeserialized("\"Emoji 😀 and e\\u0301\"", "Emoji 😀 and e\u0301");
        }

        [Fact]
        public void Deserialize_Guid()
        {
            var expected = Guid.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF");
            AssertDeserialized("\"6f9619ff-8b86-d011-b42d-00c04fc964ff\"", expected);
        }

        [Fact]
        public void Deserialize_DateTime()
        {
            var expected = DateTime.Parse("2024-01-02T03:04:05Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            AssertDeserialized("\"2024-01-02T03:04:05Z\"", expected);
        }

        [Fact]
        public void Deserialize_TimeSpan()
        {
            var expected = TimeSpan.Parse("01:02:03", CultureInfo.InvariantCulture);
            AssertDeserialized("\"01:02:03\"", expected);
        }

        [Fact]
        public void Deserialize_JsonFragment()
        {
            var expected = new JsonFragment("{\"a\":1}");
            AssertDeserialized("{\"a\":1}", expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("-128", (sbyte)-128)]
        [InlineData("127", (sbyte)127)]
        public void Deserialize_NullableSbyte(string json, sbyte? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("0", (byte)0)]
        [InlineData("255", (byte)255)]
        public void Deserialize_NullableByte(string json, byte? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("-32768", (short)-32768)]
        [InlineData("32767", (short)32767)]
        public void Deserialize_NullableShort(string json, short? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("0", (ushort)0)]
        [InlineData("65535", (ushort)65535)]
        public void Deserialize_NullableUshort(string json, ushort? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("0", 0u)]
        [InlineData("4294967295", uint.MaxValue)]
        public void Deserialize_NullableUint(string json, uint? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("-9223372036854775808", long.MinValue)]
        [InlineData("9223372036854775807", long.MaxValue)]
        public void Deserialize_NullableLong(string json, long? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("0", 0ul)]
        [InlineData("18446744073709551615", ulong.MaxValue)]
        public void Deserialize_NullableUlong(string json, ulong? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("0", 0f)]
        [InlineData("1.5", 1.5f)]
        public void Deserialize_NullableFloat(string json, float? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("\"NaN\"", null)]
        public void Deserialize_NullableFloat_SpecialCases(string json, float? expected)
        {
            if (json == "\"NaN\"")
            {
                var deserializer = new FeatureJsonDeserializer();
                Assert.True(deserializer.TryDeserialize(json, out float? value));
                Assert.True(value.HasValue);
                Assert.True(float.IsNaN(value.Value));
                return;
            }

            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("0", 0d)]
        [InlineData("1.5", 1.5d)]
        public void Deserialize_NullableDouble(string json, double? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("\"NaN\"", null)]
        public void Deserialize_NullableDouble_SpecialCases(string json, double? expected)
        {
            if (json == "\"NaN\"")
            {
                var deserializer = new FeatureJsonDeserializer();
                Assert.True(deserializer.TryDeserialize(json, out double? value));
                Assert.True(value.HasValue);
                Assert.True(double.IsNaN(value.Value));
                return;
            }

            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(0.0, "0")]
        [InlineData(1.25, "1.25")]
        public void Deserialize_NullableDecimal_AsDouble(double? expected, string json)
        {
            decimal? decimalExpected = expected.HasValue ? (decimal?)expected.Value : null;
            AssertDeserialized(json, decimalExpected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("\"A\"", 'A')]
        [InlineData("\"\\n\"", '\n')]
        public void Deserialize_NullableChar(string json, char? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("\"6f9619ff-8b86-d011-b42d-00c04fc964ff\"", "6F9619FF-8B86-D011-B42D-00C04FC964FF")]
        public void Deserialize_NullableGuid(string json, string expectedText)
        {
            Guid? expected = expectedText == null ? null : Guid.Parse(expectedText);
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("\"2024-01-02T03:04:05Z\"", "2024-01-02T03:04:05Z")]
        public void Deserialize_NullableDateTime(string json, string expectedText)
        {
            DateTime? expected = expectedText == null ? null : DateTime.Parse(expectedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("\"01:02:03\"", "01:02:03")]
        public void Deserialize_NullableTimeSpan(string json, string expectedText)
        {
            TimeSpan? expected = expectedText == null ? null : TimeSpan.Parse(expectedText, CultureInfo.InvariantCulture);
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("0", TestEnum.Zero)]
        [InlineData("2", TestEnum.Two)]
        public void Deserialize_Enum_FromNumber(string json, TestEnum expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("\"Zero\"", TestEnum.Zero)]
        [InlineData("\"Two\"", TestEnum.Two)]
        public void Deserialize_Enum_FromString(string json, TestEnum expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("1", TestEnum.One)]
        public void Deserialize_NullableEnum_FromNumber(string json, TestEnum? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData("\"One\"", TestEnum.One)]
        public void Deserialize_NullableEnum_FromString(string json, TestEnum? expected)
        {
            AssertDeserialized(json, expected);
        }

        [Fact]
        public void Deserialize_IntPtr()
        {
            var expected = new IntPtr(123);
            AssertDeserialized("123", expected);
        }

        [Fact]
        public void Deserialize_UIntPtr()
        {
            var expected = new UIntPtr(456u);
            AssertDeserialized("456", expected);
        }

        [Fact]
        public void Deserialize_ByteSegment_FromBase64()
        {
            var expected = new ByteSegment(new byte[] { 1, 2, 3, 4 });
            AssertDeserialized("\"AQIDBA==\"", expected);
        }

        [Fact]
        public void Deserialize_ByteSegment_FromArray()
        {
            var expected = new ByteSegment(new byte[] { 1, 2, 3, 4 });
            AssertDeserialized("[1,2,3,4]", expected);
        }

        [Fact]
        public void Deserialize_NullableByteSegment_Null()
        {
            ByteSegment? expected = null;
            AssertDeserialized("null", expected);
        }

        [Fact]
        public void Deserialize_ArraySegmentByte_FromBase64()
        {
            var expected = new ArraySegment<byte>(new byte[] { 1, 2, 3, 4 });
            AssertDeserialized("\"AQIDBA==\"", expected);
        }

        [Fact]
        public void Deserialize_ArraySegmentByte_FromArray()
        {
            var expected = new ArraySegment<byte>(new byte[] { 1, 2, 3, 4 });
            AssertDeserialized("[1,2,3,4]", expected);
        }

        [Fact]
        public void Deserialize_NullableArraySegmentByte_Null()
        {
            ArraySegment<byte>? expected = null;
            AssertDeserialized("null", expected);
        }

        [Fact]
        public void Deserialize_TextSegment()
        {
            var expected = new TextSegment("hello");
            AssertDeserialized("\"hello\"", expected);
        }

        [Fact]
        public void Deserialize_TextSegment_AsObject()
        {
            var expected = new TextSegment("__hello__", 2, 5);
            const string json = "{\"text\":\"__hello__\",\"startIndex\":2,\"length\":5}";
            AssertDeserialized(json, expected);
        }

        [Fact]
        public void Deserialize_ByteSegment_AsObject()
        {
            var expected = new ByteSegment(new byte[] { 1, 2, 3, 4 }, 1, 2);
            const string json = "{\"segment\":{\"_array\":[1,2,3,4],\"_offset\":1,\"_count\":2},\"hashCode\":null}";
            AssertDeserialized(json, expected);
        }

        [Fact]
        public void Deserialize_ArraySegmentByte_AsObject()
        {
            var expected = new ArraySegment<byte>(new byte[] { 1, 2, 3, 4 }, 1, 2);
            const string json = "{\"_array\":[1,2,3,4],\"_offset\":1,\"_count\":2}";
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("\"2024-01-02T03:04:05+02:30\"")]
        [InlineData("\"2024-01-02T03:04:05\"")]
        [InlineData("\"2024-01-02T03:04:05.1234567Z\"")]
        public void Deserialize_DateTime_EdgeCases(string json)
        {
            var expected = DateTime.Parse(json.Trim('"'), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("\"2.03:04:05\"", "2.03:04:05")]
        [InlineData("\"-00:00:01\"", "-00:00:01")]
        [InlineData("\"00:00:00.1234567\"", "00:00:00.1234567")]
        public void Deserialize_TimeSpan_EdgeCases(string json, string expectedText)
        {
            var expected = TimeSpan.Parse(expectedText, CultureInfo.InvariantCulture);
            AssertDeserialized(json, expected);
        }

        [Theory]
        [InlineData("\"\\u0001\"", '\u0001')]
        [InlineData("\"\\r\"", '\r')]
        public void Deserialize_Char_EdgeCases(string json, char expected)
        {
            AssertDeserialized(json, expected);
        }

        public enum TestEnum
        {
            Zero = 0,
            One = 1,
            Two = 2
        }
    }
}