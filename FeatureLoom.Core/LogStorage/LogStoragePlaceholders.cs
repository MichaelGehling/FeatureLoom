using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.LogStorage;

/// <summary>Default file-based log writer. Not implemented yet.</summary>
public sealed class LogWriter : ILogWriter
{
    private readonly LogStorageOptions options;

    /// <summary>Opens or creates the log described by <paramref name="options"/>.</summary>
    /// <param name="options">Log configuration; its schema must be the schema of <paramref name="payloadTypes"/>.</param>
    /// <param name="payloadTypes">Prepared payload types; only these can be appended.</param>
    public LogWriter(LogStorageOptions options, LogPayloadTypeRegistry payloadTypes)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        PayloadTypes = payloadTypes ?? throw new ArgumentNullException(nameof(payloadTypes));
        if (options.Schema != payloadTypes.Schema) throw new ArgumentException("The payload types must be registered for the schema of the options.", nameof(payloadTypes));
    }

    public LogIndexSchema Schema => options.Schema;
    public LogPayloadTypeRegistry PayloadTypes { get; }

    public LogEntryId Append<T>(T payload) => throw new NotImplementedException();
    public Task<LogEntryId> AppendAsync<T>(T payload, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> TryDeleteAsync(LogEntryId id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task FlushAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task RolloverAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public void Dispose() { }
}

/// <summary>Default file-based log reader. Not implemented yet.</summary>
public sealed class LogReader : ILogReader
{
    /// <summary>Opens the log in <paramref name="directory"/> for reading. Also works on read-only archives.</summary>
    /// <param name="directory">Folder of the log.</param>
    /// <param name="payloadTypes">Prepared payload types used to decode payloads.</param>
    public LogReader(string directory, LogPayloadTypeRegistry payloadTypes)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory must be set.", nameof(directory));
        Directory = directory;
        PayloadTypes = payloadTypes ?? throw new ArgumentNullException(nameof(payloadTypes));
    }

    /// <summary>Folder of the log.</summary>
    public string Directory { get; }

    public LogIndexSchema Schema => PayloadTypes.Schema;
    public LogPayloadTypeRegistry PayloadTypes { get; }

    public Task<(bool found, LogEntry<T> entry)> TryReadAsync<T>(LogEntryId id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<LogEntry<T>>> QueryAsync<T>(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<LogEntryInfo>> QueryInfoAsync(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<long> CountAsync(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
#if NETSTANDARD2_1_OR_GREATER || NET
    public IAsyncEnumerable<LogEntry<T>> QueryStreamAsync<T>(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
#endif
    public void Dispose() { }
}

/// <summary>Merges multiple logs by timestamp at query time. Not implemented yet.</summary>
public sealed class MultiLogReader
{
    private readonly ILogReader[] readers;

    /// <summary>Creates a merging reader over <paramref name="readers"/>.</summary>
    public MultiLogReader(IEnumerable<ILogReader> readers)
    {
        if (readers == null) throw new ArgumentNullException(nameof(readers));
        this.readers = new List<ILogReader>(readers).ToArray();
    }

    /// <summary>Queries all logs for entries of payload type <typeparamref name="T"/> and merges results by timestamp.</summary>
    public Task<IReadOnlyList<LogEntry<T>>> QueryAsync<T>(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
