using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests.Collections;
[CPUUsageDiagnoser]
public class TextSegmentBenchmark
{
    private TextSegment haystack;
    private TextSegment needle;
    private TextSegment prefix;
    private TextSegment suffix;
    private TextSegment equalA;
    private TextSegment equalB;
    private TextSegment padded;
    private string equalString;
    private static readonly char[] trimChars = new[]
    {
        ' ',
        '\t',
        '#'
    };
    [GlobalSetup]
    public void Setup()
    {
        string body = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation";
        string text = "xx" + body + " needle-in-haystack " + body + "yy";
        haystack = new TextSegment(text, 2, text.Length - 4);
        needle = new TextSegment("needle-in-haystack");
        prefix = haystack.SubSegment(0, 40);
        suffix = haystack.SubSegment(haystack.Length - 40);
        equalA = new TextSegment("__" + body + "__", 2, body.Length);
        equalB = new TextSegment("##" + body + "##", 2, body.Length);
        equalString = body;
        padded = new TextSegment(" \t## \t" + body + " ## \t ");
    }

    [Benchmark]
    public int IndexOfSegment()
    {
        haystack.TryFindIndex(needle, out int i);
        return i;
    }

    [Benchmark]
    public int LastIndexOfSegment()
    {
        haystack.TryFindLastIndex(needle, out int i);
        return i;
    }

    [Benchmark]
    public int IndexOfChar()
    {
        haystack.TryFindIndex('!', out int i);
        return i;
    }

    [Benchmark]
    public int LastIndexOfChar()
    {
        haystack.TryFindLastIndex('!', out int i);
        return i;
    }

    [Benchmark]
    public bool ContainsChar() => haystack.Contains('!');
    [Benchmark]
    public bool StartsWithSegment() => haystack.StartsWith(prefix);
    [Benchmark]
    public bool EndsWithSegment() => haystack.EndsWith(suffix);
    [Benchmark]
    public bool EqualsSegment() => equalA == equalB;
    [Benchmark]
    public bool EqualsString() => equalA.Equals(equalString);
    [Benchmark]
    public int HashCode()
    {
        var s = equalA;
        return s.GetHashCode();
    }

    [Benchmark]
    public TextSegment TrimChars() => padded.Trim(trimChars);
}