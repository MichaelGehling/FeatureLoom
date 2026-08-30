using FeatureLoom.Serialization;
using FeatureLoom.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerDictionaryKeySettingsTests
{
    public readonly struct Key : IEquatable<Key>
    {
        public readonly int Value;
        public Key(int value) => Value = value;
        public bool Equals(Key other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public class Holder
    {
        public Dictionary<Key, int> Configured;
        public Dictionary<Key, int> Plain;
    }

    static JsonDeserializer CreateDeserializer(Action<JsonDeserializer.Settings> configure)
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        configure(settings);
        return new JsonDeserializer(settings);
    }

    static Key ParseKey(JsonDeserializer.BufferSegment text) => new(int.Parse(text.AsString().Substring(4)));

    [Fact]
    public void ConfiguredKeyParserReadsObjectPropertyNames()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Dictionary<Key, int>>(ts =>
            ts.ConfigureObjectKey<Key>(ParseKey)));

        Assert.True(deserializer.TryDeserialize("{\"key-1\":10,\"key-2\":20}", out Dictionary<Key, int> value));
        Assert.Equal(10, value[new Key(1)]);
        Assert.Equal(20, value[new Key(2)]);
    }

    [Fact]
    public void KeyParserReceivesDecodedPropertyName()
    {
        string received = null;
        var deserializer = CreateDeserializer(s => s.ConfigureType<Dictionary<Key, int>>(ts =>
            ts.ConfigureObjectKey<Key>(text =>
            {
                received = text.AsString();
                return new Key(text.AsString().Length);
            })));

        Assert.True(deserializer.TryDeserialize("{\"a\\u002Db\":1}", out Dictionary<Key, int> value));
        Assert.Equal("a-b", received);
        Assert.Equal(1, value[new Key(3)]);
    }

    [Fact]
    public void ByteSegmentKeyParserReceivesRawUtf8PropertyNameBytes()
    {
        ByteSegment received = default;
        var deserializer = CreateDeserializer(s => s.ConfigureType<Dictionary<Key, int>>(ts =>
            ts.ConfigureObjectKey<Key>(fragment =>
            {
                received = fragment.Bytes;
                return new Key(fragment.Bytes.Count);
            })));

        Assert.True(deserializer.TryDeserialize("{\"a\\u002Db\":1}", out Dictionary<Key, int> value));
        Assert.Equal("a\\u002Db", Encoding.UTF8.GetString(received.ToArray()));
        Assert.Equal(1, value[new Key(8)]);
    }

#if !NETSTANDARD2_0
    [Fact]
    public void SpanKeyParserReceivesRawUtf8PropertyNameBytes()
    {
        int receivedLength = 0;
        var deserializer = CreateDeserializer(s => s.ConfigureType<Dictionary<Key, int>>(ts =>
            ts.ConfigureObjectKey<Key>(fragment =>
            {
                var bytes = fragment.Span;
                receivedLength = bytes.Length;
                return new Key(bytes[0]);
            })));

        Assert.True(deserializer.TryDeserialize("{\"abc\":1}", out Dictionary<Key, int> value));
        Assert.Equal(3, receivedLength);
        Assert.Equal(1, value[new Key((byte)'a')]);
    }
#endif

    [Fact]
    public void PairArrayKeysContinueThroughNormalKeyReader()
    {
        bool parserCalled = false;
        var deserializer = CreateDeserializer(s => s.ConfigureType<Dictionary<int, string>>(ts =>
            ts.ConfigureObjectKey<int>(_ =>
            {
                parserCalled = true;
                return 99;
            })));

        const string json = "[{\"key\":2,\"value\":\"x\"}]";
        Assert.True(deserializer.TryDeserialize(json, out Dictionary<int, string> value));
        Assert.Equal("x", value[2]);
        Assert.False(parserCalled);
    }

    [Fact]
    public void MemberLocalKeyParserDoesNotLeakToSiblingDictionary()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Holder>(ts =>
            ts.ConfigureMember<Dictionary<Key, int>>(nameof(Holder.Configured), member =>
                member.ConfigureObjectKey<Key>(ParseKey))));

        const string json = "{\"Configured\":{\"key-3\":30},\"Plain\":[]}";
        Assert.True(deserializer.TryDeserialize(json, out Holder value));
        Assert.Equal(30, value.Configured[new Key(3)]);
        Assert.Empty(value.Plain);
    }

    [Fact]
    public void OpenGenericKeyParserAppliesOnlyToMatchingKeyType()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureGenericType(typeof(Dictionary<,>), ts =>
            ts.ConfigureObjectKey<Key>(ParseKey)));

        Assert.True(deserializer.TryDeserialize("{\"key-4\":40}", out Dictionary<Key, int> configured));
        Assert.True(deserializer.TryDeserialize("{\"plain\":50}", out Dictionary<string, int> plain));
        Assert.Equal(40, configured[new Key(4)]);
        Assert.Equal(50, plain["plain"]);
    }

    [Fact]
    public void ExactKeyParserOverridesOpenGenericParserRegardlessOfRegistrationOrder()
    {
        foreach (bool exactFirst in new[] { false, true })
        {
            var settings = new JsonDeserializer.Settings();
            Action generic = () => settings.ConfigureGenericType(typeof(Dictionary<,>), ts =>
                ts.ConfigureObjectKey<Key>(_ => new Key(1)));
            Action exact = () => settings.ConfigureType<Dictionary<Key, int>>(ts =>
                ts.ConfigureObjectKey<Key>(_ => new Key(2)));
            if (exactFirst) { exact(); generic(); }
            else { generic(); exact(); }

            var deserializer = new JsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize("{\"x\":7}", out Dictionary<Key, int> value));
            Assert.Equal(7, value[new Key(2)]);
        }
    }

    [Fact]
    public void KeyParserAppliesWhenPopulatingDictionary()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Dictionary<Key, int>>(ts =>
            ts.ConfigureObjectKey<Key>(ParseKey)));
        var value = new Dictionary<Key, int>();

        Assert.True(deserializer.TryPopulate("{\"key-5\":50}", value));
        Assert.Equal(50, value[new Key(5)]);
    }

    [Fact]
    public void NullKeyParserRemovesConfiguration()
    {
        var settings = new JsonDeserializer.Settings();
        settings.ConfigureType<Dictionary<int, string>>(ts => ts.ConfigureObjectKey<int>(_ => 99));
        settings.ConfigureType<Dictionary<int, string>>(ts => ts.ConfigureObjectKey<int>(null));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"6\":\"x\"}", out Dictionary<int, string> value));
        Assert.Equal("x", value[6]);
    }

    [Fact]
    public void ClosedDictionaryKeyTypeMismatchThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<Exception>(() => settings.ConfigureType<Dictionary<int, string>>(ts =>
            ts.ConfigureObjectKey<string>(text => text.AsString())));
    }

    [Fact]
    public void NonDictionaryKeyConfigurationThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<Exception>(() => settings.ConfigureType<List<int>>(ts =>
            ts.ConfigureObjectKey<int>(text => int.Parse(text.AsString()))));
    }

    [Fact]
    public void ParserExceptionFollowsDeserializerExceptionPolicy()
    {
        var settings = new JsonDeserializer.Settings
        {
            rethrowExceptions = false,
            logCatchedExceptions = false
        };
        settings.ConfigureType<Dictionary<Key, int>>(ts =>
            ts.ConfigureObjectKey<Key>(_ => throw new FormatException()));
        var deserializer = new JsonDeserializer(settings);

        Assert.False(deserializer.TryDeserialize("{\"invalid\":1}", out Dictionary<Key, int> value));
        Assert.Null(value);
    }
}
