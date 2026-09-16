using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Helpers;

public class LazyValueConcurrencyTests : IDisposable
{
    private sealed class Value
    {
        public static Action Constructing;
        public readonly int Initialized;

        public Value()
        {
            Constructing?.Invoke();
            Initialized = 42;
        }
    }

    private sealed class Holder
    {
        public LazyValue<Value> Lazy;
    }

    public void Dispose() => Value.Constructing = null;

    [Fact]
    public void SnapshotReadsDoNotConstruct()
    {
        int constructions = 0;
        Value.Constructing = () => Interlocked.Increment(ref constructions);
        var holder = new Holder();

        Assert.False(holder.Lazy.Exists);
        Assert.Null(holder.Lazy.ObjIfExists);
        Assert.Equal(0, constructions);
    }

    [Fact]
    public void InitializedAccessReusesPublishedValue()
    {
        int constructions = 0;
        Value.Constructing = () => Interlocked.Increment(ref constructions);
        var holder = new Holder();

        var first = holder.Lazy.Obj;

        Assert.Equal(42, first.Initialized);
        Assert.Same(first, holder.Lazy.Obj);
        Assert.Same(first, holder.Lazy.ObjIfExists);
        Assert.True(holder.Lazy.Exists);
        Assert.Equal(1, constructions);
    }

    [Fact]
    public void AssignmentReplacesValueAndNullClearsIt()
    {
        var holder = new Holder();
        var first = holder.Lazy.Obj;
        var replacement = new Value();

        holder.Lazy.Obj = replacement;

        Assert.Same(replacement, holder.Lazy.ObjIfExists);
        Assert.Same(replacement, holder.Lazy.Obj);
        holder.Lazy.Obj = null;
        Assert.False(holder.Lazy.Exists);
        Assert.Null(holder.Lazy.ObjIfExists);
        var recreated = holder.Lazy.Obj;
        Assert.NotSame(first, recreated);
        Assert.NotSame(replacement, recreated);
    }

    [Fact]
    public void ConstructorFailureIsNotCached()
    {
        int constructions = 0;
        Value.Constructing = () =>
        {
            if (Interlocked.Increment(ref constructions) == 1) throw new InvalidOperationException("Constructor failure");
        };
        var holder = new Holder();

        var exception = Assert.ThrowsAny<Exception>(() => { _ = holder.Lazy.Obj; });

        Assert.IsType<InvalidOperationException>(exception.GetBaseException());
        Assert.Equal("Constructor failure", exception.GetBaseException().Message);
        Assert.False(holder.Lazy.Exists);
        Assert.Equal(42, holder.Lazy.Obj.Initialized);
        Assert.Equal(2, constructions);
    }

    [Fact]
    public async Task CompetingConstructorsReturnTheSamePublishedValue()
    {
        const int workers = 4;
        int constructions = 0;
        using var barrier = new Barrier(workers);
        Value.Constructing = () =>
        {
            Interlocked.Increment(ref constructions);
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)), "Constructors did not overlap");
        };
        var holder = new Holder();
        var tasks = Enumerable.Range(0, workers).Select(_ => RunOnThread(() => holder.Lazy.Obj)).ToArray();

        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(workers, constructions);
        Assert.All(results, value =>
        {
            Assert.Same(results[0], value);
            Assert.Equal(42, value.Initialized);
        });
        Assert.Same(results[0], holder.Lazy.ObjIfExists);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutationDuringConstructionFollowsPublicationOrder(bool replace)
    {
        var holder = new Holder();
        var replacement = new Value();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Value.Constructing = () =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)), "Constructor was not released");
        };
        var construction = RunOnThread(() => holder.Lazy.Obj);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Constructor was not entered");
            if (replace) holder.Lazy.Obj = replacement;
            else holder.Lazy.RemoveObj();
            Assert.Same(replace ? replacement : null, holder.Lazy.ObjIfExists);
        }
        finally
        {
            release.Set();
            await construction.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var result = await construction;
        Assert.NotNull(result);
        Assert.Equal(42, result.Initialized);
        if (replace) Assert.Same(replacement, result);
        else Assert.NotSame(replacement, result);
        Assert.Same(result, holder.Lazy.ObjIfExists);
    }

    [Fact]
    public async Task ObjNeverReturnsNullWhileAnotherThreadRemovesValues()
    {
        const int readers = 3;
        var holder = new Holder();
        int remaining = readers;
        using var barrier = new Barrier(readers + 1);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tasks = new Task<int>[readers + 1];
        for (int reader = 0; reader < readers; reader++)
        {
            tasks[reader] = RunOnThread(() =>
            {
                int nullResults = 0;
                try
                {
                    Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
                    for (int i = 0; i < 200000 && !stop.IsCancellationRequested; i++)
                    {
                        if (holder.Lazy.Obj == null) nullResults++;
                    }
                    return nullResults;
                }
                finally
                {
                    Interlocked.Decrement(ref remaining);
                }
            });
        }
        tasks[readers] = RunOnThread(() =>
        {
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            while (Volatile.Read(ref remaining) != 0 && !stop.IsCancellationRequested) holder.Lazy.RemoveObj();
            return 0;
        });

        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.False(stop.IsCancellationRequested, "Concurrent access exceeded the time limit");
        Assert.Equal(0, results.Sum());
    }

    [Fact]
    public void StructCopiesHaveIndependentStorage()
    {
        var original = new LazyValue<Value>();
        var copy = original;
        var copiedValue = copy.Obj;

        Assert.False(original.Exists);
        Assert.Same(copiedValue, copy.Obj);
        Assert.NotSame(copiedValue, original.Obj);
        copy.RemoveObj();
        Assert.True(original.Exists);
        Assert.False(copy.Exists);
    }

    [Fact]
    public void ExchangeReturnsThePreviousReferenceWithoutConstructing()
    {
        var holder = new Holder();
        var first = new Value();
        var second = new Value();
        Value.Constructing = () => throw new InvalidOperationException("Exchange must not construct");

        Assert.Null(holder.Lazy.ExchangeObj(null));
        Assert.Null(holder.Lazy.ExchangeObj(first));
        Assert.Same(first, holder.Lazy.ExchangeObj(second));
        Assert.Same(second, holder.Lazy.ObjIfExists);
        Assert.Same(second, holder.Lazy.ExchangeObj(null));
        Assert.False(holder.Lazy.Exists);
        Assert.Null(holder.Lazy.ObjIfExists);
    }

    [Fact]
    public async Task ConcurrentExchangesEachDetachOneReference()
    {
        const int workers = 8;
        var initial = new Value();
        var replacements = Enumerable.Range(0, workers).Select(_ => new Value()).ToArray();
        var holder = new Holder { Lazy = new LazyValue<Value>(initial) };
        using var barrier = new Barrier(workers);
        var tasks = replacements.Select(replacement => RunOnThread(() =>
        {
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            return holder.Lazy.ExchangeObj(replacement);
        })).ToArray();

        var detached = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        var allReferences = detached.Append(holder.Lazy.ObjIfExists).ToArray();
        Assert.Equal(workers + 1, allReferences.Distinct().Count());
        Assert.Contains(initial, allReferences);
        Assert.All(replacements, replacement => Assert.Contains(replacement, allReferences));
    }

    private static Task<T> RunOnThread<T>(Func<T> action) => Task.Factory.StartNew(action,
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
