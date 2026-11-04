using System;
using System.IO;
using FeatureLoom.Collections;

namespace FeatureLoom.LogStorage;

/// <summary>Payload codec built from delegates, so no dedicated codec class is needed.</summary>
public sealed class DelegateLogEntryCodec<T> : ILogEntryCodec<T>
{
    private readonly Action<T, Stream> encode;
    private readonly Func<ByteSegment, T> decode;

    /// <summary>Creates a codec from <paramref name="encode"/> and <paramref name="decode"/>.</summary>
    /// <param name="encode">Writes the encoded payload to the stream.</param>
    /// <param name="decode">Decodes a payload; throwing an exception signals invalid data.</param>
    public DelegateLogEntryCodec(Action<T, Stream> encode, Func<ByteSegment, T> decode)
    {
        this.encode = encode ?? throw new ArgumentNullException(nameof(encode));
        this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
    }

    /// <inheritdoc/>
    public void Encode(T payload, Stream target) => encode(payload, target);

    /// <inheritdoc/>
    public bool TryDecode(ByteSegment data, out T payload)
    {
        // Decode failures are rare (corruption, incompatible format), so mapping exceptions to false is acceptable.
        try
        {
            payload = decode(data);
            return true;
        }
        catch (Exception)
        {
            payload = default;
            return false;
        }
    }
}
