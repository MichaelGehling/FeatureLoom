using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.LogStorage;

/// <summary>Converts payloads to and from bytes. Replaceable per log.</summary>
public interface ILogEntryCodec<T>
{
    /// <summary>Writes the encoded payload to <paramref name="target"/>.</summary>
    void Encode(T payload, Stream target);

    /// <summary>Decodes a payload from <paramref name="data"/>.</summary>
    bool TryDecode(ArraySegment<byte> data, out T payload);
}

/// <summary>Appends entries to a single log. Exactly one writer per log.</summary>
public interface ILogWriter<T> : IDisposable
{
    /// <summary>Schema of the log.</summary>
    LogIndexSchema Schema { get; }

    /// <summary>Buffers an entry for writing and returns its id.</summary>
    LogEntryId Append(T payload, LogAppendInfo info = null);

    /// <summary>Appends an entry and waits until it is written.</summary>
    Task<LogEntryId> AppendAsync(T payload, LogAppendInfo info = null, CancellationToken cancellationToken = default);

    /// <summary>Flags an entry as deleted.</summary>
    Task<bool> TryDeleteAsync(LogEntryId id, CancellationToken cancellationToken = default);

    /// <summary>Writes all buffered entries.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the active segment and starts a new one.</summary>
    Task RolloverAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads and queries entries. Readers may run concurrently with the writer.</summary>
public interface ILogReader<T> : IDisposable
{
    /// <summary>Schema of the log (of the newest segment for multi-segment logs).</summary>
    LogIndexSchema Schema { get; }

    /// <summary>Reads a single entry by id.</summary>
    Task<(bool found, LogEntry<T> entry)> TryReadAsync(LogEntryId id, CancellationToken cancellationToken = default);

    /// <summary>Queries entries including payloads.</summary>
    Task<IReadOnlyList<LogEntry<T>>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Queries index metadata only, without reading payloads.</summary>
    Task<IReadOnlyList<LogEntryInfo>> QueryInfoAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>Counts matching entries using the index only.</summary>
    Task<long> CountAsync(LogQuery query, CancellationToken cancellationToken = default);

#if NETSTANDARD2_1_OR_GREATER || NET
    /// <summary>Streams matching entries.</summary>
    IAsyncEnumerable<LogEntry<T>> QueryStreamAsync(LogQuery query, CancellationToken cancellationToken = default);
#endif
}
