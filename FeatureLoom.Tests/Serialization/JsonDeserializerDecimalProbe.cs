using System;
using System.Globalization;
using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerDecimalProbe
{
    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("1.25")]
    [InlineData("12345.6789")]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335")]
    [InlineData("0.1")]
    [InlineData("00123.4500")]
    [InlineData("1e5")]
    [InlineData("1E+5")]
    [InlineData("1.5e-3")]
    [InlineData("123456789012345678901234567.89")]
    [InlineData("2.7182818284590452353602874")]
    public void Deserialize_Decimal_IsExact(string json)
    {
        decimal expected = decimal.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture);
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out decimal actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Deserialize_Decimal_Array_IsExact()
    {
        string json = "[1,-1,0.5,79228162514264337593543950335,0.0000000000000000000000000001,12345.6789]";
        decimal[] expected =
        {
            1m, -1m, 0.5m, 79228162514264337593543950335m, 0.0000000000000000000000000001m, 12345.6789m
        };
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out decimal[] actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Deserialize_Decimal_OverflowFallsBack()
    {
        // Beyond decimal range: must not throw out of the fast path in an uncontrolled way.
        var deserializer = new JsonDeserializer();
        deserializer.TryDeserialize("1e30", out decimal _);
    }
}
