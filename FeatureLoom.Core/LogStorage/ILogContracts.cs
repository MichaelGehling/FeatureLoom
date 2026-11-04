using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Collections;

namespace FeatureLoom.LogStorage;

/// <summary>Converts payloads to and from bytes. Replaceable per log.</summary>
public interface ILogEntryCodec<T>
{
    /// <summary>Writes the encoded payload to <paramref name="target"/>.</summary>
    void Encode(T payload, Stream target);

    /// <summary>Decodes a payload from <paramref name="data"/>.</summary>
    bool TryDecode(ByteSegment data, out T payload);
}

/// <summary>
/// Appends entries to a single log. Exactly one writer per log.
/// Payload types are resolved via <see cref="LogPayloadTypeRegistry.GetOrRegister{T}"/>: prepared types derive index metadata
/// from the payload; unprepared types are registered implicitly with JSON encoding if allowed, otherwise rejected.
/// </summary>
public interface ILogWriter : IDisposable
{
    /// <summary>Schema of the log.</summary>
    LogIndexSchema Schema { get; }

    /// <summary>Prepared payload types of the log.</summary>
    LogPayloadTypeRegistry PayloadTypes { get; }

    /// <summary>Buffers an entry for writing and returns its id.</summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a prepared payload type.</exception>
    LogEntryId Append<T>(T payload);

    /// <summary>Appends an entry and waits until it is written.</summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a prepared payload type.</exception>
    Task<LogEntryId> AppendAsync<T>(T payload, CancellationToken cancellationToken = default);

    /// <summary>Flags an entry as deleted.</summary>
    Task<bool> TryDeleteAsync(LogEntryId id, CancellationToken cancellationToken = default);

    /// <summary>Writes all buffered entries.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the active segment and starts a new one.</summary>
    Task RolloverAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads and queries entries. Readers may run concurrently with the writer.
/// Payload queries for <c>T</c> only return entries of the prepared payload type of <c>T</c>.
/// </summary>
public interface ILogReader : IDisposable
{
    /// <summary>Schema of the log (of the newest segment for multi-segment logs).</summary>
    LogIndexSchema Schema { get; }

    /// <summary>Prepared payload types of the log.</summary>
    LogPayloadTypeRegistry PayloadTypes { get; }

    /// <summary>Reads a single entry by id. Not found if the entry is of a different payload type.</summary>
    Task<(bool found, LogEntry<T> entry)> TryReadAsync<T>(LogEntryId id, CancellationToken cancellationToken = default);

    /// <summary>Queries entries of payload type <typeparamref name="T"/> including payloads.</summary>
    Task<IReadOnlyList<LogEntry<T>>> QueryAsync<T>(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Queries index metadata of all payload types, without reading payloads.</summary>
    Task<IReadOnlyList<LogEntryInfo>> QueryInfoAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Counts matching entries using the index only.</summary>
    Task<long> CountAsync(LogQuery query, CancellationToken cancellationToken = default);

#if NETSTANDARD2_1_OR_GREATER || NET
    /// <summary>Streams matching entries of payload type <typeparamref name="T"/>.</summary>
    IAsyncEnumerable<LogEntry<T>> QueryStreamAsync<T>(LogQuery query, CancellationToken cancellationToken = default);
#endif
}
