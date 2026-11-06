using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public partial class LogReaderTests
{
    [Fact]
    public async Task AppendManyAsync_CancelledWhileQueued_WritesNone()
    {
        var schema = CreateSchema();
        var options = new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { MaxBufferDelay = TimeSpan.FromSeconds(10) } };
        Task<List<LogEntryId>> cancelled, kept;
        using (var cts = new CancellationTokenSource())
        using (var writer = new LogWriter(options, CreateRegistry(schema)))
        {
            cancelled = writer.AppendManyAsync(Enumerable.Range(0, 3).Select(i => new Msg { Level = i, Time = T0 }), cts.Token);
            kept = writer.AppendManyAsync(Enumerable.Range(10, 2).Select(i => new Msg { Level = i, Time = T0 }));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        }
        Assert.Equal(2, (await kept).Count);
        using var reader = CreateReader(schema);
        var levels = (await reader.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level).ToArray();
        Assert.Equal(new[] { 10, 11 }, levels);
    }
}
