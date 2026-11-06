using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public partial class LogReaderTests
{
    [Fact]
    public async Task AppendManyAsync_LazyEnumerable_IsEnumeratedOnCall()
    {
        var schema = CreateSchema();
        var options = new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { MaxBufferDelay = TimeSpan.FromSeconds(10) } };
        int enumerated = 0;
        IEnumerable<Msg> Lazy(int count) { for (int i = 0; i < count; i++) { enumerated++; yield return new Msg { Level = i, Time = T0 }; } }
        Task<List<LogEntryId>> many, single, empty;
        using (var writer = new LogWriter(options, CreateRegistry(schema)))
        {
            many = writer.AppendManyAsync(Lazy(3));
            single = writer.AppendManyAsync(Lazy(1));
            empty = writer.AppendManyAsync(Lazy(0));
            Assert.Equal(4, enumerated);
            Assert.True(empty.IsCompleted);
        }
        Assert.Equal(3, (await many).Count);
        Assert.Single(await single);
        Assert.Empty(await empty);
        using var reader = CreateReader(schema);
        Assert.Equal(4, (await reader.QueryAsync<Msg>(new LogQuery())).Count);
    }
    [Fact]
    public async Task AppendAsync_WithMaxQueuedEntries_BlocksAndWritesAll()
    {
        var schema = CreateSchema();
        var options = new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { MaxQueuedEntries = 2, MaxBufferDelay = TimeSpan.FromMilliseconds(5) } };
        var tasks = new List<Task<LogEntryId>>();
        using (var writer = new LogWriter(options, CreateRegistry(schema)))
        {
            var producers = Enumerable.Range(0, 4).Select(p => Task.Run(() =>
            {
                var local = new List<Task<LogEntryId>>();
                for (int i = 0; i < 50; i++) local.Add(writer.AppendAsync(new Msg { Level = i, Time = T0 }));
                return local;
            })).ToArray();
            foreach (var p in producers) tasks.AddRange(await p);
            await Task.WhenAll(tasks);
        }
        Assert.Equal(200, tasks.Select(t => t.Result).Distinct().Count());
        using var reader = CreateReader(schema);
        Assert.Equal(200, (await reader.QueryAsync<Msg>(new LogQuery())).Count);
    }
    [Fact]
    public async Task AppendManyAsync_WithMaxQueuedEntries_CountsEntriesAndAdmitsOversizedBatch()
    {
        var schema = CreateSchema();
        var options = new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { MaxQueuedEntries = 5, MaxBufferDelay = TimeSpan.FromMilliseconds(5) } };
        var tasks = new List<Task<List<LogEntryId>>>();
        using (var writer = new LogWriter(options, CreateRegistry(schema)))
        {
            for (int b = 0; b < 10; b++) tasks.Add(writer.AppendManyAsync(Enumerable.Range(0, 3).Select(i => new Msg { Level = i, Time = T0 }).ToArray()));
            tasks.Add(writer.AppendManyAsync(Enumerable.Range(0, 12).Select(i => new Msg { Level = i, Time = T0 }).ToArray()));
            await Task.WhenAll(tasks);
        }
        Assert.Equal(42, tasks.Sum(t => t.Result.Count));
        using var reader = CreateReader(schema);
        Assert.Equal(42, (await reader.QueryAsync<Msg>(new LogQuery())).Count);
    }

    [Fact]
    public async Task Append_InterleavedWithDeleteAndReopen_KeepsAppendOffsets()
    {
        var schema = CreateSchema();
        using (var writer = CreateWriter(schema))
        {
            var first = writer.Append(new Msg { Level = 1, Time = T0.AddSeconds(5) }); // rewrites header (base time)
            writer.Append(new Msg { Level = 2, Time = T0.AddSeconds(6) });
            Assert.True(await writer.TryDeleteAsync(first));
            writer.Append(new Msg { Level = 3, Time = T0.AddSeconds(7) });
        }
        using (var writer = CreateWriter(schema))
        {
            writer.AppendMany(new[] { new Msg { Level = 4, Time = T0.AddSeconds(8) }, new Msg { Level = 5, Time = T0.AddSeconds(9), User = "u" } });
        }
        using var reader = CreateReader(schema);
        var levels = (await reader.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level).ToArray();
        Assert.Equal(new[] { 2, 3, 4, 5 }, levels);
    }
}
