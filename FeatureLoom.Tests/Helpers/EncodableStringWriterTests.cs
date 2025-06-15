using System.Text;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class EncodableStringWriterTests
{
    [Fact]
    public void DefaultConstructor_UsesUtf8Encoding()
    {
        using var writer = new EncodableStringWriter();
        Assert.Equal(Encoding.UTF8.WebName, writer.Encoding.WebName);
    }

    [Fact]
    public void Constructor_WithEncoding_UsesProvidedEncoding()
    {
        using var writer = new EncodableStringWriter(Encoding.Unicode);
        Assert.Equal(Encoding.Unicode.WebName, writer.Encoding.WebName);
    }

    [Fact]
    public void Constructor_WithNullEncoding_FallsBackToUtf8()
    {
        using var writer = new EncodableStringWriter(null);
        Assert.Equal(Encoding.UTF8.WebName, writer.Encoding.WebName);
    }

    [Fact]
    public void Constructor_WithStringBuilderAndEncoding_UsesBoth()
    {
        var sb = new StringBuilder();
        using var writer = new EncodableStringWriter(sb, Encoding.ASCII);
        writer.Write("abc");
        Assert.Equal("abc", sb.ToString());
        Assert.Equal(Encoding.ASCII.WebName, writer.Encoding.WebName);
    }

    [Fact]
    public void Constructor_WithStringBuilderAndNullEncoding_FallsBackToUtf8()
    {
        var sb = new StringBuilder();
        using var writer = new EncodableStringWriter(sb, null);
        writer.Write("xyz");
        Assert.Equal("xyz", sb.ToString());
        Assert.Equal(Encoding.UTF8.WebName, writer.Encoding.WebName);
    }

    [Fact]
    public void WrittenContent_IsAccessibleViaToString()
    {
        using var writer = new EncodableStringWriter();
        writer.Write("test123");
        Assert.Equal("test123", writer.ToString());
    }
}