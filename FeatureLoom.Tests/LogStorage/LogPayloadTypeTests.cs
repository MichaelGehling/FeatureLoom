using System;
using System.IO;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public class LogPayloadTypeTests
{
    private sealed class Msg
    {
        public DateTime Time;
        public int Level;
        public string User;
        public bool IsError;
    }

    private sealed class Other { }

    private sealed class DummyCodec<T> : ILogEntryCodec<T>
    {
        public void Encode(T payload, Stream target) { }
        public bool TryDecode(FeatureLoom.Collections.ByteSegment data, out T payload) { payload = default; return false; }
    }

    private static LogIndexSchema CreateSchema() => new LogIndexSchema(
        flagNames: new[] { "error", "audit" },
        fields: new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1), new LogFieldDefinition("user", LogFieldKind.Hash, 4), new LogFieldDefinition("source", LogFieldKind.Raw, 1) },
        typeIdSize: 1);

    private static LogPayloadType<Msg> CreateMsgType() => new LogPayloadType<Msg>(new DummyCodec<Msg>(), "msg")
        .Timestamp(m => m.Time)
        .Field("level", m => (ulong)m.Level)
        .Field("user", m => m.User)
        .Field("source", 4UL)
        .Flag("error", m => m.IsError)
        .Flag("audit");

    [Fact]
    public void Extract_DerivesMetadataFromPayload()
    {
        var schema = CreateSchema();
        var registry = new LogPayloadTypeRegistry(schema).Register(CreateMsgType());
        var type = registry.Get<Msg>();
        var metadata = new LogEntryMetadata(schema);
        var time = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        type.Extract(new Msg { Time = time, Level = 3, User = "bob", IsError = true }, DateTime.UtcNow, metadata);

        schema.TryGetFlagMask("error", out ulong error);
        schema.TryGetFlagMask("audit", out ulong audit);
        Assert.Equal(0, metadata.TypeId);
        Assert.Equal(time, metadata.Timestamp);
        Assert.Equal(error | audit, metadata.Flags);
        Assert.Equal(3UL, metadata.GetValue(0));
        Assert.Equal("bob", metadata.GetSource(1));
        Assert.Equal(4UL, metadata.GetValue(2));

        type.Extract(new Msg { Time = time, User = null }, DateTime.UtcNow, metadata);
        Assert.Equal(audit, metadata.Flags);
        Assert.Null(metadata.GetSource(1));
    }

    [Fact]
    public void Extract_UsesNowWithoutTimestampFunc()
    {
        var schema = CreateSchema();
        var type = new LogPayloadType<Other>(new DummyCodec<Other>());
        new LogPayloadTypeRegistry(schema).Register(type);
        var metadata = new LogEntryMetadata(schema);
        var now = new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc);
        type.Extract(new Other(), now, metadata);
        Assert.Equal(now, metadata.Timestamp);
        Assert.Equal(0UL, metadata.Flags);
        Assert.Equal(0UL, metadata.GetValue(0));
    }

    [Fact]
    public void UnregisteredType_IsRejected()
    {
        var registry = new LogPayloadTypeRegistry(CreateSchema()).Register(CreateMsgType());
        Assert.False(registry.TryGet<Other>(out _));
        Assert.Throws<InvalidOperationException>(() => registry.Get<Other>());
        var unregistered = new LogPayloadType<Other>(new DummyCodec<Other>());
        Assert.Throws<InvalidOperationException>(() => unregistered.Extract(new Other(), DateTime.UtcNow, new LogEntryMetadata(CreateSchema())));
    }

    [Fact]
    public void Register_ValidatesAgainstSchema()
    {
        var schema = CreateSchema();
        Assert.Throws<ArgumentException>(() => new LogPayloadTypeRegistry(schema).Register(new LogPayloadType<Msg>(new DummyCodec<Msg>()).Field("unknown", 1UL)));
        Assert.Throws<ArgumentException>(() => new LogPayloadTypeRegistry(schema).Register(new LogPayloadType<Msg>(new DummyCodec<Msg>()).Field("user", 1UL)));
        Assert.Throws<ArgumentException>(() => new LogPayloadTypeRegistry(schema).Register(new LogPayloadType<Msg>(new DummyCodec<Msg>()).Field("level", "x")));
        Assert.Throws<ArgumentException>(() => new LogPayloadTypeRegistry(schema).Register(new LogPayloadType<Msg>(new DummyCodec<Msg>()).Flag("unknown")));

        // TypeIdSize 0 allows exactly one type (id 0).
        var single = new LogPayloadTypeRegistry(new LogIndexSchema()).Register(new LogPayloadType<Msg>(new DummyCodec<Msg>()));
        Assert.Throws<ArgumentException>(() => single.Register(new LogPayloadType<Other>(new DummyCodec<Other>())));
    }

    [Fact]
    public void GetOrRegister_RegistersImplicitTypeWithJsonCodec()
    {
        var registry = new LogPayloadTypeRegistry(CreateSchema());
        var type = registry.GetOrRegister<Msg>();
        Assert.Same(type, registry.GetOrRegister<Msg>());
        Assert.IsType<JsonLogEntryCodec<Msg>>(type.Codec);
        Assert.True(registry.TypeMap.TryGetId(type.Name, out ushort id));
        Assert.Equal(id, type.TypeId);

        var metadata = new LogEntryMetadata(registry.Schema);
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        type.Extract(new Msg { Level = 5, IsError = true }, now, metadata);
        Assert.Equal(now, metadata.Timestamp);
        Assert.Equal(0UL, metadata.Flags);
        Assert.Equal(0UL, metadata.GetValue(0));
    }

    [Fact]
    public void GetOrRegister_RejectsWhenImplicitTypesDisabled()
    {
        var registry = new LogPayloadTypeRegistry(CreateSchema(), allowImplicitTypes: false);
        Assert.Throws<InvalidOperationException>(() => registry.GetOrRegister<Msg>());
        registry.Register(CreateMsgType());
        Assert.NotNull(registry.GetOrRegister<Msg>());
    }

    public class JsonPayload
    {
        public int number;
        public string text;
    }

    [Fact]
    public void JsonCodec_RoundTrips()
    {
        var codec = new JsonLogEntryCodec<JsonPayload>();
        var stream = new MemoryStream();
        stream.WriteByte(0xEE); // offset handling
        codec.Encode(new JsonPayload { number = 42, text = "hä" }, stream);
        var bytes = stream.ToArray();
        Assert.True(codec.TryDecode(new ArraySegment<byte>(bytes, 1, bytes.Length - 1), out var decoded));
        Assert.Equal(42, decoded.number);
        Assert.Equal("hä", decoded.text);
    }

    [Fact]
    public void DelegateCodec_RoundTripsAndMapsExceptions()
    {
        var type = new LogPayloadType<string>(
            (s, stream) => { var b = System.Text.Encoding.UTF8.GetBytes(s); stream.Write(b, 0, b.Length); },
            data => data.Count == 0 ? throw new InvalidDataException() : data.ToString(),
            "text");
        Assert.Equal("text", type.Name);
        var stream = new MemoryStream();
        type.Codec.Encode("hi", stream);
        Assert.True(type.Codec.TryDecode(new ArraySegment<byte>(stream.ToArray()), out var decoded));
        Assert.Equal("hi", decoded);
        Assert.False(type.Codec.TryDecode(new ArraySegment<byte>(new byte[0]), out _));
        Assert.Throws<ArgumentNullException>(() => new LogPayloadType<string>(null, data => ""));
    }

    [Fact]
    public void Name_DefaultsToSimplifiedTypeName()
    {
        var type = new LogPayloadType<Other>(new DummyCodec<Other>());
        Assert.Equal(FeatureLoom.Helpers.TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(Other)), type.Name);
    }

    [Fact]
    public void TypeIds_StayStableViaPersistedMap()
    {
        var schema = CreateSchema();
        var first = new LogPayloadTypeRegistry(schema)
            .Register(new LogPayloadType<Msg>(new DummyCodec<Msg>()))
            .Register(new LogPayloadType<Other>(new DummyCodec<Other>()));
        Assert.True(first.TypeMap.IsDirty);
        var bytes = first.TypeMap.ToBytes();
        Assert.False(first.TypeMap.IsDirty);

        // Reversed registration order must keep the ids.
        var second = new LogPayloadTypeRegistry(schema, LogPayloadTypeMap.Parse(bytes))
            .Register(new LogPayloadType<Other>(new DummyCodec<Other>()))
            .Register(new LogPayloadType<Msg>(new DummyCodec<Msg>()));
        Assert.Equal(first.Get<Msg>().TypeId, second.Get<Msg>().TypeId);
        Assert.Equal(first.Get<Other>().TypeId, second.Get<Other>().TypeId);
        Assert.False(second.TypeMap.IsDirty);
    }

    [Fact]
    public void TypeMap_NeverReusesIds()
    {
        var map = new LogPayloadTypeMap();
        map.TryAdd("a", 255, out _);
        map.TryAdd("b", 255, out ushort b);
        var reloaded = LogPayloadTypeMap.Parse(map.ToBytes());
        Assert.True(reloaded.TryAdd("c", 255, out ushort c));
        Assert.Equal(b + 1, c);
        Assert.True(reloaded.TryGetName(b, out string name));
        Assert.Equal("b", name);
        Assert.False(reloaded.TryAdd("d", 2, out _));
    }

    [Fact]
    public void TypeMap_DetectsCorruption()
    {
        var map = new LogPayloadTypeMap();
        map.TryAdd("abc", 255, out _);
        var bytes = map.ToBytes();
        bytes[12] ^= 1;
        Assert.Throws<InvalidDataException>(() => LogPayloadTypeMap.Parse(bytes));
        Assert.Throws<InvalidDataException>(() => LogPayloadTypeMap.Parse(new byte[5]));
    }

    [Fact]
    public void TypeMap_WritesAndLoadsFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), "FeatureLoomTypeMap_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal(0, LogPayloadTypeMap.Load(dir).Count);
            var map = new LogPayloadTypeMap();
            map.TryAdd("x", 255, out _);
            map.Write(dir);
            map.TryAdd("y", 255, out _);
            map.Write(dir);
            var loaded = LogPayloadTypeMap.Load(dir);
            Assert.True(loaded.TryGetId("y", out ushort y));
            Assert.Equal(1, y);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Register_RejectsDuplicatesAndFreezes()
    {
        var registry = new LogPayloadTypeRegistry(CreateSchema());
        var msgType = CreateMsgType();
        registry.Register(msgType);
        Assert.Throws<ArgumentException>(() => registry.Register(new LogPayloadType<Other>(new DummyCodec<Other>(), "msg")));
        Assert.Throws<ArgumentException>(() => registry.Register(new LogPayloadType<Msg>(new DummyCodec<Msg>(), "msg2")));
        Assert.Throws<InvalidOperationException>(() => msgType.Field("level", 1UL));

        registry.Register(new LogPayloadType<Other>(new DummyCodec<Other>(), "other"));
        Assert.True(registry.TryGet(1, out var byId));
        Assert.Equal(typeof(Other), byId.PayloadType);
        Assert.True(registry.TryGet("msg", out var byName));
        Assert.Same(msgType, byName);
    }
}
