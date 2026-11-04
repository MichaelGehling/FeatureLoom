using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.LogStorage;

/// <summary>Default file-based log writer. Not implemented yet.</summary>
public sealed class LogWriter<T> : ILogWriter<T>
{
    private readonly LogStorageOptions options;
    private readonly ILogEntryCodec<T> codec;

    /// <summary>Opens or creates the log described by <paramref name="options"/>.</summary>
    public LogWriter(LogStorageOptions options, ILogEntryCodec<T> codec)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        if (options.Schema == null) throw new ArgumentException("Schema must be set.", nameof(options));
    }

    public LogIndexSchema Schema => options.Schema;

    public LogEntryId Append(T payload, LogAppendInfo info = null) => throw new NotImplementedException();
    public Task<LogEntryId> AppendAsync(T payload, LogAppendInfo info = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<bool> TryDeleteAsync(LogEntryId id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task FlushAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task RolloverAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public void Dispose() { }
}

/// <summary>Default file-based log reader. Not implemented yet.</summary>
public sealed class LogReader<T> : ILogReader<T>
{
    private readonly ILogEntryCodec<T> codec;

    /// <summary>Opens the log in <paramref name="directory"/> for reading. Also works on read-only archives.</summary>
    public LogReader(string directory, ILogEntryCodec<T> codec)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory must be set.", nameof(directory));
        Directory = directory;
        this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    /// <summary>Folder of the log.</summary>
    public string Directory { get; }

    public LogIndexSchema Schema => throw new NotImplementedException();

    public Task<(bool found, LogEntry<T> entry)> TryReadAsync(LogEntryId id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<LogEntry<T>>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<LogEntryInfo>> QueryInfoAsync(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<long> CountAsync(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
#if NETSTANDARD2_1_OR_GREATER || NET
    public IAsyncEnumerable<LogEntry<T>> QueryStreamAsync(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
#endif
    public void Dispose() { }
}

/// <summary>Merges multiple logs by timestamp at query time. Not implemented yet.</summary>
public sealed class MultiLogReader<T>
{
    private readonly ILogReader<T>[] readers;

    /// <summary>Creates a merging reader over <paramref name="readers"/>.</summary>
    public MultiLogReader(IEnumerable<ILogReader<T>> readers)
    {
        if (readers == null) throw new ArgumentNullException(nameof(readers));
        this.readers = new List<ILogReader<T>>(readers).ToArray();
    }

    /// <summary>Queries all logs and merges results by timestamp.</summary>
    public Task<IReadOnlyList<LogEntry<T>>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
