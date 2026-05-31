using BenchmarkDotNet.Attributes;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    [MaxIterationCount(100)]
    [MinIterationCount(80)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    //[RPlotExporter]
    [HtmlExporter]
    public class SetOnWaitingAsyncTest
    {
        FeatureLoom.Synchronization.AsyncManualResetEvent _amre1 = new FeatureLoom.Synchronization.AsyncManualResetEvent();
        FeatureLoom.Synchronization.AsyncManualResetEvent _amre2 = new FeatureLoom.Synchronization.AsyncManualResetEvent();
        Nito.AsyncEx.AsyncManualResetEvent _asyncEx1 = new Nito.AsyncEx.AsyncManualResetEvent(false);
        Nito.AsyncEx.AsyncManualResetEvent _asyncEx2 = new Nito.AsyncEx.AsyncManualResetEvent(false);
        ManualResetEventSlim _mres1 = new ManualResetEventSlim(false);
        ManualResetEventSlim _mres2 = new ManualResetEventSlim(false);
        ManualResetEvent _mre1 = new ManualResetEvent(false);
        ManualResetEvent _mre2 = new ManualResetEvent(false);

        [IterationSetup(Target = nameof(AsyncManualResetEvent0))]
        public void ItSetup_AsyncManualResetEvent0() => PrepareAmre();

        [IterationSetup(Target = nameof(AsyncManualResetEvent))]
        public void ItSetup_AsyncManualResetEvent() => PrepareAmre();

        private void PrepareAmre()
        {
            _amre1.Reset(); _amre2.Reset();
            Task.Run(async () => { Task t = _amre1.WaitAsync(); _amre2.Set(); await t; });
            _amre2.Wait();
            AppTime.WaitPrecisely(1.Milliseconds());
        }

        [IterationSetup(Target = nameof(AsyncExManualResetEvent))]
        public void ItSetup_AsyncExManualResetEvent()
        {
            _asyncEx1.Reset(); _asyncEx2.Reset();
            Task.Run(async () => { Task t = _asyncEx1.WaitAsync(); _asyncEx2.Set(); await t; });
            _asyncEx2.WaitAsync().Wait();
            AppTime.WaitPrecisely(1.Milliseconds());
        }

        [IterationSetup(Target = nameof(ManualResetEventSlim))]
        public void ItSetup_ManualResetEventSlim()
        {
            _mres1.Reset(); _mres2.Reset();
            Task.Run(async () => { Task t = _mres1.WaitHandle.WaitOneAsync(CancellationToken.None); _mres2.Set(); await t; });
            _mres2.Wait();
            AppTime.WaitPrecisely(1.Milliseconds());
        }

        [IterationSetup(Target = nameof(ManualResetEvent))]
        public void ItSetup_ManualResetEvent()
        {
            _mre1.Reset(); _mre2.Reset();
            Task.Run(async () => { Task t = _mre1.WaitOneAsync(CancellationToken.None); _mre2.Set(); await t; });
            _mre2.WaitOne();
            AppTime.WaitPrecisely(1.Milliseconds());
        }

        [Benchmark]
        public void AsyncManualResetEvent0() => _amre1.Set();

        [Benchmark(Baseline = true)]
        public void AsyncManualResetEvent() => _amre1.Set();

        [Benchmark]
        public void AsyncExManualResetEvent() => _asyncEx1.Set();

        [Benchmark]
        public void ManualResetEventSlim() => _mres1.Set();

        [Benchmark]
        public void ManualResetEvent() => _mre1.Set();
    }
}
