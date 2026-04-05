using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System;
using System.Globalization;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerPrimitiveTests
    {
        private static void AssertSerialized<T>(T value, string expected)
        {
            var serializer = new JsonSerializer();
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        private static void AssertSerialized<T>(T value, string expected, JsonSerializer.Settings settings)
        {
            var serializer = new JsonSerializer(settings);
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        private static string FormatExpectedDateTime(DateTime value)
        {
            if (value == default) return "\"0001-01-01T00:00:00\"";

            string text = value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            int fractionalTicks = (int)(value.Ticks % TimeSpan.TicksPerSecond);
            if (fractionalTicks > 0)
            {
                text += "." + fractionalTicks.ToString("D7", CultureInfo.InvariantCulture);
            }

            if (value.Kind == DateTimeKind.Utc)
            {
                text += "Z";
            }
            else if (value.Kind == DateTimeKind.Local)
            {
                TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(value);
                int hours = (int)Math.Abs(offset.TotalHours);
                int minutes = Math.Abs(offset.Minutes);
                string sign = offset.Ticks < 0 ? "-" : "+";
                text += $"{sign}{hours:00}:{minutes:00}";
            }

            return $"\"{text}\"";
        }

        [Fact]
        public void Serialize_NullReference_ReturnsNullLiteral()
        {
            string value = null;
            AssertSerialized(value, "null");
        }

        [Theory]
        [InlineData(true, "true")]
        [InlineData(false, "false")]
        public void Serialize_Bool(bool value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(true, "true")]
        [InlineData(false, "false")]
        public void Serialize_NullableBool(bool? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData((sbyte)-128, "-128")]
        [InlineData((sbyte)127, "127")]
        public void Serialize_Sbyte(sbyte value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData((sbyte)-128, "-128")]
        [InlineData((sbyte)127, "127")]
        public void Serialize_NullableSbyte(sbyte? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData((byte)0, "0")]
        [InlineData((byte)255, "255")]
        public void Serialize_Byte(byte value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData((byte)0, "0")]
        [InlineData((byte)255, "255")]
        public void Serialize_NullableByte(byte? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData((short)-32768, "-32768")]
        [InlineData((short)32767, "32767")]
        public void Serialize_Short(short value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData((short)-32768, "-32768")]
        [InlineData((short)32767, "32767")]
        public void Serialize_NullableShort(short? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData((ushort)0, "0")]
        [InlineData((ushort)65535, "65535")]
        public void Serialize_Ushort(ushort value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData((ushort)0, "0")]
        [InlineData((ushort)65535, "65535")]
        public void Serialize_NullableUshort(ushort? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(int.MinValue, "-2147483648")]
        [InlineData(int.MaxValue, "2147483647")]
        public void Serialize_Int(int value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(int.MinValue, "-2147483648")]
        [InlineData(int.MaxValue, "2147483647")]
        public void Serialize_NullableInt(int? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(uint.MinValue, "0")]
        [InlineData(uint.MaxValue, "4294967295")]
        public void Serialize_Uint(uint value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(uint.MinValue, "0")]
        [InlineData(uint.MaxValue, "4294967295")]
        public void Serialize_NullableUint(uint? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(long.MinValue, "-9223372036854775808")]
        [InlineData(long.MaxValue, "9223372036854775807")]
        public void Serialize_Long(long value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(long.MinValue, "-9223372036854775808")]
        [InlineData(long.MaxValue, "9223372036854775807")]
        public void Serialize_NullableLong(long? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(ulong.MinValue, "0")]
        [InlineData(ulong.MaxValue, "18446744073709551615")]
        public void Serialize_Ulong(ulong value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(ulong.MinValue, "0")]
        [InlineData(ulong.MaxValue, "18446744073709551615")]
        public void Serialize_NullableUlong(ulong? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(0f, "0")]
        [InlineData(1.5f, "1.5")]
        public void Serialize_Float(float value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(float.NaN, "\"NaN\"")]
        [InlineData(float.PositiveInfinity, "\"Infinity\"")]
        [InlineData(float.NegativeInfinity, "\"-Infinity\"")]
        public void Serialize_Float_SpecialCases(float value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(0f, "0")]
        [InlineData(1.5f, "1.5")]
        public void Serialize_NullableFloat(float? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(float.NaN, "\"NaN\"")]
        [InlineData(float.PositiveInfinity, "\"Infinity\"")]
        [InlineData(float.NegativeInfinity, "\"-Infinity\"")]
        public void Serialize_NullableFloat_SpecialCases(float? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(0d, "0")]
        [InlineData(1.5d, "1.5")]
        [InlineData(1.012345678901234d, "1.012345678901234")]
        [InlineData(0.1d, "0.1")]
        [InlineData(0.01234d, "0.01234")]
        [InlineData(0.00001d, "0.00001")]
        public void Serialize_Double(double value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(1e20d, "1E20")]
        [InlineData(1e-6d, "1E-6")]
        public void Serialize_Double_ExponentNotation(double value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(double.NaN, "\"NaN\"")]
        [InlineData(double.PositiveInfinity, "\"Infinity\"")]
        [InlineData(double.NegativeInfinity, "\"-Infinity\"")]
        public void Serialize_Double_SpecialCases(double value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(0d, "0")]
        [InlineData(1.5d, "1.5")]
        public void Serialize_NullableDouble(double? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(double.NaN, "\"NaN\"")]
        [InlineData(double.PositiveInfinity, "\"Infinity\"")]
        [InlineData(double.NegativeInfinity, "\"-Infinity\"")]
        public void Serialize_NullableDouble_SpecialCases(double? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(0.0, "0")]
        [InlineData(1.25, "1.25")]
        public void Serialize_Decimal_AsDouble(decimal value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(0.0, "0")]
        [InlineData(1.25, "1.25")]
        public void Serialize_NullableDecimal_AsDouble(double? value, string expected)
        {
            decimal? decimalValue = (decimal?)value;
            AssertSerialized(decimalValue, expected);
        }

        [Theory]
        [InlineData('A', "\"A\"")]
        [InlineData('\n', "\"\\n\"")]
        [InlineData('\u0001', "\"\\u0001\"")]
        public void Serialize_Char(char value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData('A', "\"A\"")]
        [InlineData('\n', "\"\\n\"")]
        [InlineData('\u0001', "\"\\u0001\"")]
        public void Serialize_NullableChar(char? value, string expected)
        {
            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_String_EscapesSpecialCharacters()
        {
            var value = "Line1\nLine2\t\"quote\"\\backslash";
            const string expected = "\"Line1\\nLine2\\t\\\"quote\\\"\\\\backslash\"";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_String_EscapesControlCharacters()
        {
            var value = "Start\u0001End";
            const string expected = "\"Start\\u0001End\"";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Guid()
        {
            var value = Guid.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF");
            const string expected = "\"6f9619ff-8b86-d011-b42d-00c04fc964ff\"";

            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData("6F9619FF-8B86-D011-B42D-00C04FC964FF", "\"6f9619ff-8b86-d011-b42d-00c04fc964ff\"")]
        public void Serialize_NullableGuid(string valueText, string expected)
        {
            Guid? value = valueText == null ? null : Guid.Parse(valueText);
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData("0001-01-01T00:00:00", "\"0001-01-01T00:00:00\"")]
        [InlineData("2024-01-02T03:04:05Z", "\"2024-01-02T03:04:05Z\"")]
        public void Serialize_DateTime(string valueText, string expected)
        {
            var value = DateTime.Parse(valueText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_DateTime_Utc_WithFractionalSeconds()
        {
            var value = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);
            string expected = FormatExpectedDateTime(value);

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_DateTime_Unspecified_NoOffset()
        {
            var value = DateTime.SpecifyKind(new DateTime(2024, 1, 2, 3, 4, 5), DateTimeKind.Unspecified);
            string expected = FormatExpectedDateTime(value);

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_DateTime_Local_WithOffset()
        {
            var value = DateTime.SpecifyKind(new DateTime(2024, 1, 2, 3, 4, 5), DateTimeKind.Local);
            string expected = FormatExpectedDateTime(value);

            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData("2024-01-02T03:04:05Z", "\"2024-01-02T03:04:05Z\"")]
        public void Serialize_NullableDateTime(string valueText, string expected)
        {
            DateTime? value = valueText == null ? null : DateTime.Parse(valueText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_NullableDateTime_Local_WithOffset()
        {
            DateTime? value = DateTime.SpecifyKind(new DateTime(2024, 1, 2, 3, 4, 5), DateTimeKind.Local);
            string expected = FormatExpectedDateTime(value.Value);

            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData("00:00:00", "\"00:00:00\"")]
        [InlineData("01:02:03", "\"01:02:03\"")]
        [InlineData("2.03:04:05", "\"2.03:04:05\"")]
        public void Serialize_TimeSpan(string valueText, string expected)
        {
            var value = TimeSpan.Parse(valueText, CultureInfo.InvariantCulture);
            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData("01:02:03", "\"01:02:03\"")]
        public void Serialize_NullableTimeSpan(string valueText, string expected)
        {
            TimeSpan? value = valueText == null ? null : TimeSpan.Parse(valueText, CultureInfo.InvariantCulture);
            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_JsonFragment_Raw()
        {
            var value = new JsonFragment("{\"a\":1}");
            const string expected = "{\"a\":1}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_JsonFragment_Invalid_WritesNull()
        {
            var value = new JsonFragment(null);
            const string expected = "null";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_NullableJsonFragment_Null()
        {
            JsonFragment? value = null;
            const string expected = "null";

            AssertSerialized(value, expected);
        }

        [Theory]
        [InlineData(TestEnum.Zero, "0")]
        [InlineData(TestEnum.Two, "2")]
        public void Serialize_Enum_AsNumber(TestEnum value, string expected)
        {
            AssertSerialized(value, expected, new JsonSerializer.Settings { enumAsString = false });
        }

        [Theory]
        [InlineData(TestEnum.Zero, "\"Zero\"")]
        [InlineData(TestEnum.Two, "\"Two\"")]
        public void Serialize_Enum_AsString(TestEnum value, string expected)
        {
            AssertSerialized(value, expected, new JsonSerializer.Settings { enumAsString = true });
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(TestEnum.One, "1")]
        public void Serialize_NullableEnum_AsNumber(TestEnum? value, string expected)
        {
            AssertSerialized(value, expected, new JsonSerializer.Settings { enumAsString = false });
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData(TestEnum.One, "\"One\"")]
        public void Serialize_NullableEnum_AsString(TestEnum? value, string expected)
        {
            AssertSerialized(value, expected, new JsonSerializer.Settings { enumAsString = true });
        }

        [Fact]
        public void Serialize_String_UnicodeAndSurrogates()
        {
            var value = "Emoji 😀 and e\u0301";
            const string expected = "\"Emoji 😀 and e\u0301\"";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_TextSegment()
        {
            var value = new TextSegment("__hello__", 2, 5);
            const string expected = "\"hello\"";

            AssertSerialized(value, expected);
        }

        public enum TestEnum
        {
            Zero = 0,
            One = 1,
            Two = 2
        }
    }
}