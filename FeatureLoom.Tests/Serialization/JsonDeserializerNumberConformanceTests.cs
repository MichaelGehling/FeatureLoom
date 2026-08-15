using System;
using System.Globalization;
using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

/// <summary>
/// Conformance regression tests for RFC 8259 numbers, which place no limit on the number of
/// digits a number may contain.
/// <para>
/// Historically the shared digit reader threw ("Number is too large" / "Too many digits in
/// number") as soon as a number carried 20 or more digits, regardless of whether the value was
/// representable in the target type or was merely being skipped. Digit count and target range
/// are separate concerns and these tests keep them separate.
/// </para>
/// </summary>
public class JsonDeserializerNumberConformanceTests
{
    [Theory]
    // Long fraction runs must be accepted and rounded, not rejected.
    [InlineData("3.141592653589793238462643383279")]
    [InlineData("0.12345678901234567890123456789012345678901234567890")]
    [InlineData("1.00000000000000000000000000000000000001")]
    // Long integer runs that stay in double range.
    [InlineData("123456789012345678901234567890")]
    [InlineData("-123456789012345678901234567890")]
    // Long runs combined with exponents.
    [InlineData("1.2345678901234567890123456789e10")]
    [InlineData("12345678901234567890123456789e-20")]
    // Extreme digit counts, far past any accumulator limit.
    [InlineData("1000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("0.0000000000000000000000000000000000000000000000000000000000001")]
    public void Deserialize_Double_AcceptsArbitraryDigitCounts(string json)
    {
        double expected = double.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture);
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out double actual));
        // Allow a small relative tolerance: the parser is not a correctly-rounded strtod.
        Assert.True(RelativeError(expected, actual) < 1e-12,
            $"expected {expected:R} but got {actual:R}");
    }

    /// <summary>
    /// Regression: digits dropped from the integer part must shift the scale, not vanish.
    /// Dropping them silently would turn a huge number into a small one.
    /// </summary>
    [Fact]
    public void Deserialize_Double_DroppedIntegerDigits_PreserveMagnitude()
    {
        // 40 significant digits: well past what a ulong accumulator can hold.
        const string json = "1234567890123456789012345678901234567890";
        double expected = double.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture);
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out double actual));
        Assert.True(RelativeError(expected, actual) < 1e-12,
            $"expected {expected:R} but got {actual:R}");
    }

    [Fact]
    public void SkipValue_AcceptsArbitraryDigitCounts()
    {
        // The oversized number is in a field that is not bound by the target type, so it is
        // skipped. Skipping must not fail on digit count.
        string json = "{\"known\":1,\"ignored\":123456789012345678901234567890.123456789012345678901234567890}";
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out KnownOnly result));
        Assert.Equal(1, result.known);
    }

    /// <summary>
    /// Regression: a skipped number that exceeds every numeric type must still be skipped
    /// cleanly, and parsing must continue correctly with the field that follows it.
    /// </summary>
    [Fact]
    public void SkipValue_OversizedNumber_ContinuesWithFollowingField()
    {
        string json = "{\"ignored\":" + new string('9', 400) + ",\"known\":42}";
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out KnownOnly result));
        Assert.Equal(42, result.known);
    }

    public class KnownOnly
    {
        public int known;
    }

    [Theory]
    [InlineData("123456789012345678901234567890")] // 30 digits, exceeds long
    [InlineData("99999999999999999999")]           // 20 digits, exceeds long
    [InlineData("9223372036854775808")]            // long.MaxValue + 1
    [InlineData("-9223372036854775809")]           // long.MinValue - 1
    public void Deserialize_Long_OutOfRange_Fails(string json)
    {
        // Oversized integers are a target-range error and may be rejected, but must be reported
        // as such rather than as a digit-count failure.
        var deserializer = new JsonDeserializer();
        Assert.False(deserializer.TryDeserialize(json, out long _));
    }

    /// <summary>
    /// Regression: leading zeros inflate the digit count without changing the value. A
    /// digit-count based limit would reject these; a range check must accept them.
    /// </summary>
    [Theory]
    [InlineData("0000000000000000000000000000042", 42L)]
    [InlineData("-0000000000000000000000000000042", -42L)]
    [InlineData("0000000000009223372036854775807", long.MaxValue)]
    public void Deserialize_Long_LeadingZeros_AreAccepted(string json, long expected)
    {
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out long value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Deserialize_Long_MaxValue_RoundTrips()
    {
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize("9223372036854775807", out long value));
        Assert.Equal(long.MaxValue, value);
    }

    [Fact]
    public void Deserialize_Long_MinValue_RoundTrips()
    {
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize("-9223372036854775808", out long value));
        Assert.Equal(long.MinValue, value);
    }

    [Fact]
    public void Deserialize_ULong_MaxValue_RoundTrips()
    {
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize("18446744073709551615", out ulong value));
        Assert.Equal(ulong.MaxValue, value);
    }

    [Fact]
    public void Deserialize_ULong_OutOfRange_Fails()
    {
        var deserializer = new JsonDeserializer();
        Assert.False(deserializer.TryDeserialize("18446744073709551616", out ulong _));
    }

    /// <summary>
    /// Regression: numbers deserialized into <see cref="object"/> go through a separate code
    /// path that also used the shared digit reader, so it needs the same coverage.
    /// </summary>
    [Theory]
    [InlineData("123456789012345678901234567890")]
    [InlineData("1.23456789012345678901234567890")]
    [InlineData("0.0000000000000000000000000000001")]
    public void Deserialize_AsObject_AcceptsArbitraryDigitCounts(string json)
    {
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out object value));
        Assert.NotNull(value);
    }

    // Accuracy of double parsing is covered by JsonDeserializerDoubleAccuracyTests.

    private static double RelativeError(double expected, double actual)
    {
        if (expected == actual) return 0;
        if (expected == 0) return Math.Abs(actual);
        return Math.Abs((actual - expected) / expected);
    }
}
