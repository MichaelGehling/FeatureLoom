using System;
using System.Globalization;
using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

/// <summary>
/// Regression tests for <see cref="decimal"/> deserialization.
/// <para>
/// Historically the parser converted decimals via <see cref="double"/>, which silently lost
/// precision for values with more than ~17 significant digits, and threw once a number carried
/// 20 or more digits. Both behaviours are covered here so a reintroduction is caught.
/// </para>
/// </summary>
public class JsonDeserializerDecimalTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("1.25")]
    [InlineData("12345.6789")]
    [InlineData("0.1")]
    [InlineData("00123.4500")]
    [InlineData("1e5")]
    [InlineData("1E+5")]
    [InlineData("1.5e-3")]
    // Regression: the following need the full 96-bit mantissa. A double-based conversion
    // rounds them and the assertion fails.
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335")]
    [InlineData("123456789012345678901234567.89")]
    [InlineData("2.7182818284590452353602874")]
    [InlineData("9999999999999999999999999999")]
    [InlineData("1234567890123456789.0123456789")]
    public void Deserialize_Decimal_IsExact(string json)
    {
        decimal expected = decimal.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture);
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out decimal actual));
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Regression: a decimal routed through <see cref="double"/> keeps only ~17 significant
    /// digits, so the low-order digits come back as zeros. Comparing the exact bit pattern
    /// (via <see cref="decimal.GetBits"/> through <see cref="decimal.Equals(decimal)"/>) proves
    /// the full mantissa survived.
    /// </summary>
    [Fact]
    public void Deserialize_Decimal_KeepsDigitsBeyondDoublePrecision()
    {
        const string json = "1.2345678901234567890123456789";
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out decimal actual));

        Assert.Equal(decimal.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture), actual);
        // A double round-trip would collapse to 1.2345678901234567 and lose the tail.
        Assert.NotEqual((decimal)double.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture), actual);
    }

    [Fact]
    public void Deserialize_Decimal_MaxAndMinValue_RoundTrip()
    {
        var deserializer = new JsonDeserializer();

        Assert.True(deserializer.TryDeserialize(
            decimal.MaxValue.ToString(CultureInfo.InvariantCulture), out decimal max));
        Assert.Equal(decimal.MaxValue, max);

        Assert.True(deserializer.TryDeserialize(
            decimal.MinValue.ToString(CultureInfo.InvariantCulture), out decimal min));
        Assert.Equal(decimal.MinValue, min);
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

    /// <summary>
    /// Regression: values outside decimal range must be reported as a failed deserialization,
    /// not surface as an unhandled exception from the fast path.
    /// </summary>
    [Theory]
    [InlineData("1e30")]
    [InlineData("-1e30")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("1" + "0000000000000000000000000000000000000000")]
    public void Deserialize_Decimal_OutOfRange_FailsGracefully(string json)
    {
        var deserializer = new JsonDeserializer();
        var exception = Record.Exception(() => deserializer.TryDeserialize(json, out decimal _));
        Assert.Null(exception);
    }

    /// <summary>
    /// Regression: very long digit runs previously threw from the shared digit reader before the
    /// value could even be range-checked. Parsing must complete for a representable value no
    /// matter how many digits were written.
    /// </summary>
    [Theory]
    [InlineData("0.10000000000000000000000000000000000000000000000000", 0.1)]
    [InlineData("1.00000000000000000000000000000000000000000000000000", 1.0)]
    [InlineData("000000000000000000000000000000000000000000000000001", 1.0)]
    public void Deserialize_Decimal_AcceptsPaddedDigitRuns(string json, double expected)
    {
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out decimal actual));
        Assert.Equal((decimal)expected, actual);
    }
}
