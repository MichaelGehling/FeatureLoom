using System;
using System.IO;
using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Default payload codec using FeatureLoom's <see cref="JsonSerializer"/>/<see cref="JsonDeserializer"/> (compact UTF-8 JSON).
/// Thread-safe; encode and decode use separate instances so they do not block each other.
/// </summary>
public sealed class JsonLogEntryCodec<T> : ILogEntryCodec<T>
{
    private readonly JsonSerializer serializer;
    private readonly JsonDeserializer deserializer;
    // Setting the data source and deserializing must happen atomically.
    private MicroValueLock decodeLock;

    /// <summary>Creates a codec with optional custom settings (defaults: compact output, non-strict parsing).</summary>
    public JsonLogEntryCodec(JsonSerializer.Settings serializerSettings = null, JsonDeserializer.Settings deserializerSettings = null)
    {
        serializer = new JsonSerializer(serializerSettings);
        deserializer = new JsonDeserializer(deserializerSettings);
    }

    /// <inheritdoc/>
    public void Encode(T payload, Stream target) => serializer.Serialize(target, payload);

    /// <inheritdoc/>
    public bool TryDecode(ByteSegment data, out T payload)
    {
        decodeLock.Enter();
        try
        {
            deserializer.SetDataSource(data);
            return deserializer.TryDeserialize(out payload);
        }
        finally
        {
            decodeLock.Exit();
        }
    }
}
