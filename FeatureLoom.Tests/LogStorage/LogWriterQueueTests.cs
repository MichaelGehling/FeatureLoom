using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public partial class LogReaderTests
{
    [Fact]
    public async Task AppendAsync_ConcurrentCallers_AllWrittenWithUniqueIds()
    {
        var schema = CreateSchema();
        LogEntryId[] ids;
        using (var writer = CreateWriter(schema))
        {
            ids = await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => writer.AppendAsync(new Msg { Level = i, Time = T0 }))));
            var more = await writer.AppendManyAsync(Enumerable.Range(200, 50).Select(i => new Msg { Level = i, Time = T0 }));
            ids = ids.Concat(more).ToArray();
            await writer.FlushAsync();
        }
        Assert.Equal(250, ids.Distinct().Count());
        using var reader = CreateReader(schema);
        Assert.Equal(Enumerable.Range(0, 250), (await reader.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level).OrderBy(l => l));
        var (found, entry) = await reader.TryReadAsync<Msg>(ids[123]);
        Assert.True(found);
        Assert.Equal(123, entry.Payload.Level);
    }

    [Fact]
    public async Task AppendAsync_WithBufferDelay_DisposeWritesQueuedEntries()
    {
        var schema = CreateSchema();
        var options = new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { MaxBufferDelay = TimeSpan.FromSeconds(10) } };
        Task<LogEntryId>[] pending;
        using (var writer = new LogWriter(options, CreateRegistry(schema)))
        {
            pending = Enumerable.Range(0, 5).Select(i => writer.AppendAsync(new Msg { Level = i, Time = T0 })).ToArray();
        }
        await Task.WhenAll(pending);
        using var reader = CreateReader(schema);
        Assert.Equal(5, (await reader.QueryAsync<Msg>(new LogQuery())).Count);
    }

    [Fact]
    public async Task AppendAsync_CancelledWhileQueued_IsNotWritten()
    {
        var schema = CreateSchema();
        var options = new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { MaxBufferDelay = TimeSpan.FromSeconds(10) } };
        Task<LogEntryId> cancelled, kept;
        using (var cts = new CancellationTokenSource())
        using (var writer = new LogWriter(options, CreateRegistry(schema)))
        {
            Assert.True(writer.AppendAsync(new Msg { Level = 9, Time = T0 }, new CancellationToken(true)).IsCanceled);
            cancelled = writer.AppendAsync(new Msg { Level = 1, Time = T0 }, cts.Token);
            kept = writer.AppendAsync(new Msg { Level = 2, Time = T0 });
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        }
        await kept;
        using var reader = CreateReader(schema);
        var levels = (await reader.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level).ToArray();
        Assert.Equal(new[] { 2 }, levels);
    }

    [Fact]
    public async Task TryDeleteAsync_CancelledWhileDraining_Throws()
    {
        var schema = CreateSchema();
        var options = new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { MaxBufferDelay = TimeSpan.FromSeconds(10) } };
        using (var cts = new CancellationTokenSource())
        using (var writer = new LogWriter(options, CreateRegistry(schema)))
        {
            var id = writer.Append(new Msg { Level = 1, Time = T0 });
            _ = writer.AppendAsync(new Msg { Level = 2, Time = T0 });
            var delete = writer.TryDeleteAsync(id, cts.Token);
            Assert.False(delete.IsCompleted);
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delete);
        }
        using var reader = CreateReader(schema);
        Assert.Equal(2, (await reader.QueryAsync<Msg>(new LogQuery())).Count);
    }

    [Fact]
    public async Task LogWriterSink_AppendsPostedMessages()
    {
        var schema = CreateSchema();
        using (var writer = CreateWriter(schema))
        {
            var sink = new LogWriterSink<Msg>(writer);
            sink.Post(new Msg { Level = 1, Time = T0 });
            sink.Post(42);
            await sink.PostAsync(new Msg { Level = 2, Time = T0 });
            await writer.FlushAsync();
        }
        using var reader = CreateReader(schema);
        Assert.Equal(new[] { 1, 2 }, (await reader.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level).OrderBy(l => l));
    }

    [Fact]
    public async Task LogWriterSink_ReportsErrorsOfPostAndPostAsync()
    {
        var schema = CreateSchema();
        var writer = CreateWriter(schema);
        writer.Dispose();
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var sink = new LogWriterSink<Msg>(writer, errors.Enqueue);
        await sink.PostAsync(new Msg { Level = 1, Time = T0 });
        Assert.Single(errors);
        Assert.IsType<ObjectDisposedException>(errors.First());
        var silent = new LogWriterSink<Msg>(writer);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => silent.PostAsync(new Msg { Level = 1, Time = T0 }));
    }

    public struct ValueMsg
    {
        public DateTime Time;
        public int Level;
    }

    private sealed class RecordingCallback : ILogAppendCallback
    {
        public readonly System.Collections.Concurrent.ConcurrentQueue<(LogEntryId id, object state)> Appended = new System.Collections.Concurrent.ConcurrentQueue<(LogEntryId, object)>();
        public readonly System.Collections.Concurrent.ConcurrentQueue<Exception> Failed = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        public void OnAppended(LogEntryId id, object state) => Appended.Enqueue((id, state));
        public void OnFailed(Exception error, object state) => Failed.Enqueue(error);
    }

    [Fact]
    public async Task Enqueue_ValueTypePayload_ReportsIdsViaCallback()
    {
        var schema = CreateSchema();
        var callback = new RecordingCallback();
        using (var writer = CreateWriter(schema))
        {
            writer.PayloadTypes.Register(new LogPayloadType<ValueMsg>(name: "vmsg").Timestamp(m => m.Time).Field("level", m => (ulong)m.Level));
            for (int i = 0; i < 20; i++) writer.Enqueue(new ValueMsg { Level = i, Time = T0 }, callback, i);
            await writer.FlushAsync();
        }
        Assert.Equal(20, callback.Appended.Count);
        Assert.Empty(callback.Failed);
        Assert.Equal(Enumerable.Range(0, 20), callback.Appended.Select(a => (int)a.state).OrderBy(s => s));
        using var reader = new LogReader(directory, CreateRegistry(schema).Register(new LogPayloadType<ValueMsg>(name: "vmsg").Timestamp(m => m.Time).Field("level", m => (ulong)m.Level)));
        var levels = (await reader.QueryAsync<ValueMsg>(new LogQuery())).Select(e => e.Payload.Level).OrderBy(l => l);
        Assert.Equal(Enumerable.Range(0, 20), levels);
    }

    [Fact]
    public async Task LogWriterSink_PostReportsErrorToHandler()
    {
        var schema = CreateSchema();
        var writer = CreateWriter(schema);
        writer.Dispose();
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var sink = new LogWriterSink<Msg>(writer, errors.Enqueue);
        sink.Post(new Msg { Level = 1, Time = T0 });
        for (int i = 0; i < 100 && errors.IsEmpty; i++) await Task.Delay(10);
        Assert.IsType<ObjectDisposedException>(Assert.Single(errors));
    }
}
