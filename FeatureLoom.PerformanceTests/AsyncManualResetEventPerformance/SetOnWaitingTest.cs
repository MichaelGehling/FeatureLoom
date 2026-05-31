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
    public class SetOnWaitingTest
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
            Task.Run(() => { _amre2.Set(); _amre1.Wait(); });
            _amre2.Wait();
            AppTime.WaitPrecisely(1.Milliseconds());
        }

        [IterationSetup(Target = nameof(AsyncExManualResetEvent))]
        public void ItSetup_AsyncExManualResetEvent()
        {
            _asyncEx1.Reset(); _asyncEx2.Reset();
            Task.Run(() => { _asyncEx2.Set(); _asyncEx1.Wait(); });
            _asyncEx2.WaitAsync().Wait();
            AppTime.WaitPrecisely(1.Milliseconds());
        }

        [IterationSetup(Target = nameof(ManualResetEventSlim))]
        public void ItSetup_ManualResetEventSlim()
        {
            _mres1.Reset(); _mres2.Reset();
            Task.Run(() => { _mres2.Set(); _mres1.Wait(); });
            _mres2.Wait();
            AppTime.WaitPrecisely(1.Milliseconds());
        }

        [IterationSetup(Target = nameof(ManualResetEvent))]
        public void ItSetup_ManualResetEvent()
        {
            _mre1.Reset(); _mre2.Reset();
            Task.Run(() => { _mre2.Set(); _mre1.WaitOne(); });
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
