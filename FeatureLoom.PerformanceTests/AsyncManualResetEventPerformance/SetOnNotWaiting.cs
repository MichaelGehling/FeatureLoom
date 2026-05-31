using BenchmarkDotNet.Attributes;
using FeatureLoom.Synchronization;
using System.Threading;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    // Each benchmark pre-resets a batch of N events in IterationSetup, then sets them all
    // in one invocation. OperationsPerInvoke = N lets BenchmarkDotNet report per-Set() time
    // without the 1-op-per-iteration measurement overhead that IterationSetup would otherwise force.
    [MaxIterationCount(100)]
    [MinIterationCount(80)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    //[RPlotExporter]
    [HtmlExporter]
    public class SetOnNotWaitingTest
    {
        private const int N = 1024 * 100;

        private FeatureLoom.Synchronization.AsyncManualResetEvent[] _amreBatch;
        private Nito.AsyncEx.AsyncManualResetEvent[] _asyncExBatch;
        private ManualResetEventSlim[] _mresBatch;
        private ManualResetEvent[] _mreBatch;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _amreBatch   = new FeatureLoom.Synchronization.AsyncManualResetEvent[N];
            _asyncExBatch = new Nito.AsyncEx.AsyncManualResetEvent[N];
            _mresBatch   = new ManualResetEventSlim[N];
            _mreBatch    = new ManualResetEvent[N];
            for (int i = 0; i < N; i++)
            {
                _amreBatch[i]   = new FeatureLoom.Synchronization.AsyncManualResetEvent(false);
                _asyncExBatch[i] = new Nito.AsyncEx.AsyncManualResetEvent(false);
                _mresBatch[i]   = new ManualResetEventSlim(false);
                _mreBatch[i]    = new ManualResetEvent(false);
            }
        }

        [IterationSetup(Target = nameof(AsyncManualResetEvent0))]
        public void ItSetup_AsyncManualResetEvent0() { foreach (var e in _amreBatch) e.Reset(); }

        [IterationSetup(Target = nameof(AsyncManualResetEvent))]
        public void ItSetup_AsyncManualResetEvent() { foreach (var e in _amreBatch) e.Reset(); }

        [IterationSetup(Target = nameof(AsyncExManualResetEvent))]
        public void ItSetup_AsyncExManualResetEvent() { foreach (var e in _asyncExBatch) e.Reset(); }

        [IterationSetup(Target = nameof(ManualResetEventSlim))]
        public void ItSetup_ManualResetEventSlim() { foreach (var e in _mresBatch) e.Reset(); }

        [IterationSetup(Target = nameof(ManualResetEvent))]
        public void ItSetup_ManualResetEvent() { foreach (var e in _mreBatch) e.Reset(); }

        [Benchmark(OperationsPerInvoke = N)]
        public void AsyncManualResetEvent0() { foreach (var e in _amreBatch) e.Set(); }

        [Benchmark(Baseline = true, OperationsPerInvoke = N)]
        public void AsyncManualResetEvent() { foreach (var e in _amreBatch) e.Set(); }

        [Benchmark(OperationsPerInvoke = N)]
        public void AsyncExManualResetEvent() { foreach (var e in _asyncExBatch) e.Set(); }

        [Benchmark(OperationsPerInvoke = N)]
        public void ManualResetEventSlim() { foreach (var e in _mresBatch) e.Set(); }

        [Benchmark(OperationsPerInvoke = N)]
        public void ManualResetEvent() { foreach (var e in _mreBatch) e.Set(); }
    }
}
