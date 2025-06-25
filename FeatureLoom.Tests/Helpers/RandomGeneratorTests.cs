using System;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class RandomGeneratorTests
{
    [Fact]
    public void Bool_ReturnsTrueAndFalse()
    {
        bool foundTrue = false, foundFalse = false;
        for (int i = 0; i < 1000; i++)
        {
            bool value = RandomGenerator.Bool();
            if (value) foundTrue = true;
            else foundFalse = true;
            if (foundTrue && foundFalse) break;
        }
        Assert.True(foundTrue && foundFalse);
    }

    [Fact]
    public void Int32_Range_WithinBounds()
    {
        int min = -100, max = 100;
        for (int i = 0; i < 1000; i++)
        {
            int value = RandomGenerator.Int32(min, max);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void Int32_Crypto_ProducesValue()
    {
        int value = RandomGenerator.Int32(crypto: true);
        // No exception and a value is produced
        Assert.IsType<int>(value);
    }

    [Fact]
    public void Int16_Range_WithinBounds()
    {
        short min = -1000, max = 1000;
        for (int i = 0; i < 1000; i++)
        {
            short value = RandomGenerator.Int16(min, max);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void Int64_Range_WithinBounds()
    {
        long min = -100000, max = 100000;
        for (int i = 0; i < 1000; i++)
        {
            long value = RandomGenerator.Int64(min, max);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void Int64_Crypto_ProducesValue()
    {
        long value = RandomGenerator.Int64(crypto: true);
        Assert.IsType<long>(value);
    }

    [Fact]
    public void Double_Range_WithinBounds()
    {
        double min = -10.5, max = 20.5;
        for (int i = 0; i < 1000; i++)
        {
            double value = RandomGenerator.Double(min, max);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void Float_Range_WithinBounds()
    {
        float min = -5.5f, max = 5.5f;
        for (int i = 0; i < 1000; i++)
        {
            float value = RandomGenerator.Float(min, max);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void GUID_Crypto_And_NonCrypto_AreDifferent()
    {
        var guid1 = RandomGenerator.GUID();
        var guid2 = RandomGenerator.GUID(crypto: true);
        Assert.NotEqual(guid1, guid2);
    }

    [Fact]
    public void Bytes_LengthAndContent()
    {
        var bytes = RandomGenerator.Bytes(32);
        Assert.Equal(32, bytes.Length);
        Assert.Contains(bytes, b => b != 0); // Most likely not all zero
    }

    [Fact]
    public void String_LengthAndCharset()
    {
        string allowed = "ABC";
        string s = RandomGenerator.String(50, allowedChars: allowed);
        Assert.Equal(50, s.Length);
        foreach (char c in s)
        {
            Assert.Contains(c, allowed);
        }
    }
}