using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.IO;
using FeatureLoom.Collections;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Non-generic view of a prepared payload type. Only payload types registered in a <see cref="LogPayloadTypeRegistry"/> can be logged.
/// </summary>
public abstract class LogPayloadType
{
    private protected LogPayloadType(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Payload type name must not be empty.", nameof(name));
        Name = name;
    }

    /// <summary>
    /// Id stored in the index record. Assigned on registration from the log's <see cref="LogPayloadTypeMap"/>,
    /// so it stays stable for <see cref="Name"/> across the lifetime of the log.
    /// </summary>
    public ushort TypeId { get; internal set; }

    /// <summary>Stable name of the payload type; persisted in the log's type map.</summary>
    public string Name { get; }

    /// <summary>CLR type of the payload.</summary>
    public abstract Type PayloadType { get; }

    internal abstract void Validate(LogIndexSchema schema);
}

/// <summary>
/// Prepared payload type: codec plus functions that derive timestamp, flags and custom field values from a payload.
/// Fields without a configured function get the value 0 (or null source).
/// Configure all functions before registering; the definition is frozen on registration.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public sealed class LogPayloadType<T> : LogPayloadType
{
    private readonly Dictionary<string, Func<T, ulong>> valueGetters = new Dictionary<string, Func<T, ulong>>(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<T, string>> sourceGetters = new Dictionary<string, Func<T, string>>(StringComparer.Ordinal);
    private readonly List<KeyValuePair<string, Func<T, bool>>> flagGetters = new List<KeyValuePair<string, Func<T, bool>>>();
    private Func<T, DateTime> timestampGetter;
    private bool frozen;

    // Resolved on registration: index-ordered arrays avoid any lookup during append.
    private Func<T, ulong>[] resolvedValueGetters;
    private Func<T, string>[] resolvedSourceGetters;
    private Func<T, bool>[] resolvedFlagGetters;
    private ulong[] resolvedFlagMasks;

    /// <summary>Creates a payload type definition.</summary>
    /// <param name="codec">Codec used to encode/decode payloads. Defaults to <see cref="JsonLogEntryCodec{T}"/>.</param>
    /// <param name="name">
    /// Stable name persisted in the log's type map. Defaults to the simplified type name of <typeparamref name="T"/>
    /// (<see cref="TypeNameHelper"/>); set it explicitly to keep existing logs readable after renaming the CLR type.
    /// </param>
    public LogPayloadType(ILogEntryCodec<T> codec = null, string name = null) : base(name ?? TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(T)))
    {
        Codec = codec ?? new JsonLogEntryCodec<T>();
    }

    /// <summary>Creates a payload type definition with a codec built from delegates.</summary>
    /// <param name="encode">Writes the encoded payload to the stream.</param>
    /// <param name="decode">Decodes a payload from the stored bytes; throwing signals invalid data.</param>
    /// <param name="name">Stable name persisted in the log's type map. Defaults to the simplified type name of <typeparamref name="T"/>.</param>
    public LogPayloadType(Action<T, Stream> encode, Func<ByteSegment, T> decode, string name = null)
        : this(new DelegateLogEntryCodec<T>(encode, decode), name)
    {
    }

    /// <summary>Codec of the payload type.</summary>
    public ILogEntryCodec<T> Codec { get; }

    /// <inheritdoc/>
    public override Type PayloadType => typeof(T);

    /// <summary>Derives the timestamp from the payload. If not set, the write time (UTC now) is used.</summary>
    public LogPayloadType<T> Timestamp(Func<T, DateTime> getter)
    {
        CheckNotFrozen();
        timestampGetter = getter ?? throw new ArgumentNullException(nameof(getter));
        return this;
    }

    /// <summary>Sets the user flag <paramref name="flagName"/> if <paramref name="predicate"/> returns true.</summary>
    public LogPayloadType<T> Flag(string flagName, Func<T, bool> predicate)
    {
        CheckNotFrozen();
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        flagGetters.Add(new KeyValuePair<string, Func<T, bool>>(flagName, predicate));
        return this;
    }

    /// <summary>Always sets the user flag <paramref name="flagName"/> for this payload type.</summary>
    public LogPayloadType<T> Flag(string flagName) => Flag(flagName, _ => true);

    /// <summary>Derives the raw value of a Raw/Ascending/Descending field from the payload.</summary>
    public LogPayloadType<T> Field(string fieldName, Func<T, ulong> getter)
    {
        CheckNotFrozen();
        valueGetters[fieldName] = getter ?? throw new ArgumentNullException(nameof(getter));
        return this;
    }

    /// <summary>Sets a constant raw value of a Raw/Ascending/Descending field for this payload type.</summary>
    public LogPayloadType<T> Field(string fieldName, ulong constant) => Field(fieldName, _ => constant);

    /// <summary>Derives the source value of a Hash/Handle field from the payload.</summary>
    public LogPayloadType<T> Field(string fieldName, Func<T, string> getter)
    {
        CheckNotFrozen();
        sourceGetters[fieldName] = getter ?? throw new ArgumentNullException(nameof(getter));
        return this;
    }

    /// <summary>Sets a constant source value of a Hash/Handle field for this payload type.</summary>
    public LogPayloadType<T> Field(string fieldName, string constant) => Field(fieldName, (Func<T, string>)(_ => constant));

    /// <summary>Derives the index metadata of <paramref name="payload"/> into <paramref name="metadata"/>.</summary>
    /// <param name="payload">Payload to derive metadata from.</param>
    /// <param name="now">Fallback timestamp if no timestamp function is configured.</param>
    /// <param name="metadata">Container that receives the metadata; reused by callers.</param>
    /// <exception cref="InvalidOperationException">The payload type is not registered.</exception>
    public void Extract(T payload, DateTime now, LogEntryMetadata metadata)
    {
        if (!frozen) throw new InvalidOperationException($"Payload type '{Name}' is not registered.");
        metadata.TypeId = TypeId;
        metadata.Timestamp = timestampGetter != null ? timestampGetter(payload) : now;

        ulong flags = 0;
        for (int i = 0; i < resolvedFlagGetters.Length; i++)
        {
            if (resolvedFlagGetters[i](payload)) flags |= resolvedFlagMasks[i];
        }
        metadata.Flags = flags;

        for (int i = 0; i < resolvedValueGetters.Length; i++)
        {
            var valueGetter = resolvedValueGetters[i];
            if (valueGetter != null) metadata.SetValue(i, valueGetter(payload));
            else
            {
                metadata.SetValue(i, 0);
                var sourceGetter = resolvedSourceGetters[i];
                metadata.SetSource(i, sourceGetter?.Invoke(payload));
            }
        }
    }

    internal override void Validate(LogIndexSchema schema)
    {
        int fieldCount = schema.Fields.Count;
        var values = new Func<T, ulong>[fieldCount];
        var sources = new Func<T, string>[fieldCount];

        foreach (var pair in valueGetters)
        {
            int index = GetFieldIndex(schema, pair.Key);
            if (IsSourceKind(schema.Fields[index].Kind)) throw new ArgumentException($"Field '{pair.Key}' of payload type '{Name}' is a {schema.Fields[index].Kind} field and requires a string source.");
            values[index] = pair.Value;
        }
        foreach (var pair in sourceGetters)
        {
            int index = GetFieldIndex(schema, pair.Key);
            if (!IsSourceKind(schema.Fields[index].Kind)) throw new ArgumentException($"Field '{pair.Key}' of payload type '{Name}' is a {schema.Fields[index].Kind} field and requires a numeric value.");
            sources[index] = pair.Value;
        }

        var flagFuncs = new Func<T, bool>[flagGetters.Count];
        var flagMasks = new ulong[flagGetters.Count];
        for (int i = 0; i < flagGetters.Count; i++)
        {
            if (!schema.TryGetFlagMask(flagGetters[i].Key, out flagMasks[i])) throw new ArgumentException($"Flag '{flagGetters[i].Key}' of payload type '{Name}' is not defined in the schema.");
            flagFuncs[i] = flagGetters[i].Value;
        }

        resolvedValueGetters = values;
        resolvedSourceGetters = sources;
        resolvedFlagGetters = flagFuncs;
        resolvedFlagMasks = flagMasks;
        frozen = true;
    }

    private int GetFieldIndex(LogIndexSchema schema, string fieldName)
    {
        if (!schema.TryGetFieldIndex(fieldName, out int index)) throw new ArgumentException($"Field '{fieldName}' of payload type '{Name}' is not defined in the schema.");
        return index;
    }

    private void CheckNotFrozen()
    {
        if (frozen) throw new InvalidOperationException($"Payload type '{Name}' is already registered and cannot be changed.");
    }

    private static bool IsSourceKind(LogFieldKind kind) => kind == LogFieldKind.Hash || kind == LogFieldKind.Handle;
}

/// <summary>
/// Set of prepared payload types of a log. Types are validated against the schema on registration and get their
/// type id from the log's <see cref="LogPayloadTypeMap"/> (new names are added to the map).
/// Lookup by CLR type is used on append, lookup by type id on read.
/// If <see cref="AllowImplicitTypes"/> is set, unregistered types are registered on first use with the default
/// JSON codec, default name and no fields/flags; explicit registration is only needed for fields, flags,
/// a timestamp function, a custom codec or a custom name.
/// Thread-safe: lookups are lock-free (copy-on-write), registrations are serialized.
/// </summary>
public sealed class LogPayloadTypeRegistry
{
    private readonly MicroLock registerLock = new MicroLock();
    // Copy-on-write: registrations are rare, lookups happen per append/read and must not lock.
    private Dictionary<Type, LogPayloadType> byClrType = new Dictionary<Type, LogPayloadType>();
    private Dictionary<ushort, LogPayloadType> byId = new Dictionary<ushort, LogPayloadType>();
    private Dictionary<string, LogPayloadType> byName = new Dictionary<string, LogPayloadType>(StringComparer.Ordinal);

    /// <summary>Creates an empty registry for <paramref name="schema"/>.</summary>
    /// <param name="schema">Schema the types are validated against.</param>
    /// <param name="typeMap">Persisted name-to-id map of the log; a new empty map if null.</param>
    /// <param name="allowImplicitTypes">If true, unregistered types are registered implicitly on first use (see <see cref="GetOrRegister{T}"/>).</param>
    public LogPayloadTypeRegistry(LogIndexSchema schema, LogPayloadTypeMap typeMap = null, bool allowImplicitTypes = true)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        TypeMap = typeMap ?? new LogPayloadTypeMap();
        AllowImplicitTypes = allowImplicitTypes;
    }

    /// <summary>If true, unregistered types are registered on first use with default settings.</summary>
    public bool AllowImplicitTypes { get; }

    /// <summary>Schema the types are validated against.</summary>
    public LogIndexSchema Schema { get; }

    /// <summary>Name-to-id map of the log. Must be persisted if <see cref="LogPayloadTypeMap.IsDirty"/>.</summary>
    public LogPayloadTypeMap TypeMap { get; }

    /// <summary>Registered payload types.</summary>
    public IEnumerable<LogPayloadType> Types => byId.Values;

    /// <summary>Validates, freezes and registers a payload type and assigns its type id.</summary>
    /// <exception cref="ArgumentException">The type conflicts with the schema or an already registered type, or no type id is left.</exception>
    public LogPayloadTypeRegistry Register<T>(LogPayloadType<T> payloadType)
    {
        if (payloadType == null) throw new ArgumentNullException(nameof(payloadType));
        using (registerLock.Lock())
        {
            RegisterLocked(payloadType);
        }
        return this;
    }

    /// <summary>
    /// Gets the prepared payload type for <typeparamref name="T"/>, registering a default one
    /// (JSON codec, default name, no fields/flags) if <see cref="AllowImplicitTypes"/> is set. Used on append.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not prepared and implicit types are not allowed.</exception>
    /// <exception cref="ArgumentException">Implicit registration failed (name conflict or no type id left).</exception>
    public LogPayloadType<T> GetOrRegister<T>()
    {
        if (TryGet<T>(out var payloadType)) return payloadType;
        if (!AllowImplicitTypes) throw new InvalidOperationException($"Type {typeof(T)} is not a prepared payload type of this log.");
        using (registerLock.Lock())
        {
            if (TryGet<T>(out payloadType)) return payloadType;
            payloadType = new LogPayloadType<T>();
            RegisterLocked(payloadType);
            return payloadType;
        }
    }

    private void RegisterLocked<T>(LogPayloadType<T> payloadType)
    {
        if (byName.ContainsKey(payloadType.Name)) throw new ArgumentException($"Payload type name '{payloadType.Name}' is already registered.", nameof(payloadType));
        if (byClrType.ContainsKey(typeof(T))) throw new ArgumentException($"CLR type {typeof(T)} is already registered.", nameof(payloadType));

        payloadType.Validate(Schema);
        if (!TypeMap.TryGetId(payloadType.Name, out ushort typeId) && !TypeMap.TryAdd(payloadType.Name, Schema.MaxTypeId, out typeId))
            throw new ArgumentException($"No type id left for payload type '{payloadType.Name}' (type id size {Schema.TypeIdSize} B).", nameof(payloadType));
        if (typeId > Schema.MaxTypeId) throw new ArgumentException($"Type id {typeId} of payload type '{payloadType.Name}' exceeds the schema's type id size ({Schema.TypeIdSize} B).", nameof(payloadType));
        payloadType.TypeId = typeId;

        byClrType = new Dictionary<Type, LogPayloadType>(byClrType) { { typeof(T), payloadType } };
        byId = new Dictionary<ushort, LogPayloadType>(byId) { { typeId, payloadType } };
        byName = new Dictionary<string, LogPayloadType>(byName, StringComparer.Ordinal) { { payloadType.Name, payloadType } };
    }

    /// <summary>Gets the prepared payload type for <typeparamref name="T"/>. Exact type match; base types are not considered.</summary>
    public bool TryGet<T>(out LogPayloadType<T> payloadType)
    {
        if (byClrType.TryGetValue(typeof(T), out var type))
        {
            payloadType = (LogPayloadType<T>)type;
            return true;
        }
        payloadType = null;
        return false;
    }

    /// <summary>Gets the prepared payload type for <typeparamref name="T"/>.</summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not prepared for this log.</exception>
    public LogPayloadType<T> Get<T>() =>
        TryGet<T>(out var payloadType) ? payloadType : throw new InvalidOperationException($"Type {typeof(T)} is not a prepared payload type of this log.");

    /// <summary>Gets a payload type by its id.</summary>
    public bool TryGet(ushort typeId, out LogPayloadType payloadType) => byId.TryGetValue(typeId, out payloadType);

    /// <summary>Gets a payload type by its name.</summary>
    public bool TryGet(string name, out LogPayloadType payloadType) => byName.TryGetValue(name, out payloadType);
}
