using System;
using System.Globalization;
using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

/// <summary>
/// Pins the accuracy of double parsing.
/// <para>
/// The parser scales the accumulated mantissa arithmetically, which is fast but rounds at each
/// step, so results may differ from a correctly-rounded conversion by a few ULP. That is an
/// accepted trade-off: callers needing exactness beyond this should use <see cref="decimal"/> or
/// configure a custom decoder based on <see cref="double.Parse(string, IFormatProvider)"/>.
/// </para>
/// <para>
/// Values that scale into the subnormal range are the exception. There the gap between
/// representable doubles is enormous relative to the value, and the arithmetic path was once off
/// by 4.77e-04 relative (for example "53421e-324") rather than a few ULP. Those inputs now take a
/// correctly-rounded path and must stay bit-exact.
/// </para>
/// </summary>
public class JsonDeserializerDoubleAccuracyTests
{
    /// <summary>Distance between two doubles counted in representable steps.</summary>
    private static long UlpDiff(double expected, double actual)
    {
        if (expected == actual) return 0;
        if (double.IsNaN(expected) || double.IsNaN(actual)) return long.MaxValue;
        if (double.IsInfinity(expected) != double.IsInfinity(actual)) return long.MaxValue;
        long a = BitConverter.DoubleToInt64Bits(expected);
        long b = BitConverter.DoubleToInt64Bits(actual);
        if (a < 0) a = long.MinValue - a;
        if (b < 0) b = long.MinValue - b;
        return Math.Abs(a - b);
    }

    private static long WorstUlp(Func<Random, string> generate, int count, int seed, out string worstInput)
    {
        var deserializer = new JsonDeserializer();
        var random = new Random(seed);
        long worst = 0;
        worstInput = "";
        for (int i = 0; i < count; i++)
        {
            string json = generate(random);
            if (!double.TryParse(json, NumberStyles.Float, CultureInfo.InvariantCulture, out double expected)) continue;
            if (double.IsNaN(expected) || double.IsInfinity(expected)) continue;
            Assert.True(deserializer.TryDeserialize(json, out double actual), $"failed to parse {json}");
            long ulp = UlpDiff(expected, actual);
            if (ulp > worst) { worst = ulp; worstInput = json; }
        }
        return worst;
    }

    private static string Digits(Random r, int n)
    {
        var c = new char[n];
        for (int i = 0; i < n; i++) c[i] = (char)('0' + r.Next(10));
        c[0] = (char)('1' + r.Next(9));
        return new string(c);
    }

    /// <summary>
    /// Regression: subnormals must be bit-exact. "53421e-324" previously came back with 4.77e-04
    /// relative error, which is visibly the wrong number rather than a rounding artefact.
    /// </summary>
    [Theory]
    [InlineData("53421e-324")]
    [InlineData("5e-324")]
    [InlineData("4.9406564584124654e-324")]
    [InlineData("2.2250738585072014e-308")]
    [InlineData("1e-320")]
    [InlineData("-53421e-324")]
    [InlineData("123456789012345678e-320")]
    public void Deserialize_Double_ExtremeExponents_AreExact(string json)
    {
        double expected = double.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture);
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out double actual));
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Large positive exponents keep using the fast arithmetic path, so they are only bounded by
    /// a few ULP rather than exact. Overflow to infinity must still be reported faithfully.
    /// </summary>
    [Theory]
    [InlineData("1.7976931348623157e308")]
    [InlineData("-1.7976931348623157e308")]
    public void Deserialize_Double_NearMaxValue_StaysWithinFewUlp(string json)
    {
        double expected = double.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture);
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out double actual));
        Assert.True(UlpDiff(expected, actual) <= 8,
            $"{json}: {UlpDiff(expected, actual)} ULP");
    }

    /// <summary>Values below double.Epsilon underflow to a correctly signed zero.</summary>
    [Theory]
    [InlineData("1e-400", 0.0)]
    [InlineData("-1e-400", -0.0)]
    public void Deserialize_Double_Underflow_YieldsSignedZero(string json, double expected)
    {
        var deserializer = new JsonDeserializer();
        Assert.True(deserializer.TryDeserialize(json, out double actual));
        Assert.Equal(expected, actual);
        Assert.Equal(double.IsNegative(expected), double.IsNegative(actual));
    }

    [Fact]
    public void Deserialize_Double_SubnormalRange_IsExact()
    {
        long worst = WorstUlp(r => Digits(r, 17) + "e-" + r.Next(308, 325), 20000, 7, out string input);
        Assert.True(worst == 0, $"subnormal range must be bit-exact, got {worst} ULP for {input}");
    }

    [Fact]
    public void Deserialize_Double_BelowThreshold_IsExact()
    {
        long worst = WorstUlp(r => Digits(r, 17) + "e-" + r.Next(291, 308), 20000, 7, out string input);
        Assert.True(worst == 0, $"below the threshold must be bit-exact, got {worst} ULP for {input}");
    }

    /// <summary>
    /// Ordinary magnitudes use the fast arithmetic path. 8 ULP leaves headroom over the measured
    /// worst case (5) while still catching a real regression in the scaling logic.
    /// </summary>
    [Theory]
    [InlineData(1)]   // short decimals
    [InlineData(17)]  // full precision
    [InlineData(25)]  // past the accumulator limit
    [InlineData(40)]
    [InlineData(80)]
    public void Deserialize_Double_SignificantDigits_StayWithinFewUlp(int fractionDigits)
    {
        long worst = WorstUlp(r => Digits(r, 1) + "." + Digits(r, fractionDigits), 10000, 7, out string input);
        Assert.True(worst <= 8, $"{fractionDigits} fraction digits: {worst} ULP for {input}");
    }

    [Theory]
    [InlineData(-20, 20)]
    [InlineData(-100, -1)]
    [InlineData(-300, 300)]
    [InlineData(300, 309)]
    public void Deserialize_Double_ExponentRange_StaysWithinFewUlp(int minExp, int maxExp)
    {
        long worst = WorstUlp(r => Digits(r, 17) + "e" + r.Next(minExp, maxExp), 20000, 7, out string input);
        Assert.True(worst <= 8, $"exponent [{minExp},{maxExp}]: {worst} ULP for {input}");
    }

    /// <summary>
    /// Round-trip fidelity for "R"-formatted doubles. Only part of the values are bit-exact
    /// because the fast path is not correctly rounded; the bound guards against real regressions.
    /// </summary>
    [Fact]
    public void Deserialize_Double_RoundTripAccuracy()
    {
        var deserializer = new JsonDeserializer();
        var random = new Random(12345);
        int exactMatches = 0;
        int total = 0;
        long worstUlp = 0;

        for (int i = 0; i < 20000; i++)
        {
            double d = BitConverter.Int64BitsToDouble(((long)random.Next() << 32) | (uint)random.Next());
            if (double.IsNaN(d) || double.IsInfinity(d)) continue;

            string json = d.ToString("R", CultureInfo.InvariantCulture);
            if (!deserializer.TryDeserialize(json, out double parsed)) continue;

            total++;
            if (parsed == d) exactMatches++;
            worstUlp = Math.Max(worstUlp, UlpDiff(d, parsed));
        }

        double exactRatio = (double)exactMatches / total;
        Assert.True(total > 0);
        Assert.True(worstUlp <= 8, $"worst {worstUlp} ULP over {total} values");
        Assert.True(exactRatio > 0.40, $"exact round-trip ratio dropped to {exactRatio:P4}");
    }
}
