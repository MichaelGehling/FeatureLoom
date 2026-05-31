using BenchmarkDotNet.Attributes;
using FeatureLoom.Synchronization;
using System.Threading;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    [MaxIterationCount(40)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    //[RPlotExporter]
    [HtmlExporter]
    public class SetResetTest
    {
        FeatureLoom.Synchronization.AsyncManualResetEvent _amre = new FeatureLoom.Synchronization.AsyncManualResetEvent();
        Nito.AsyncEx.AsyncManualResetEvent _asyncEx = new Nito.AsyncEx.AsyncManualResetEvent(false);
        ManualResetEventSlim _mres = new ManualResetEventSlim(false);
        ManualResetEvent _mre = new ManualResetEvent(false);

        [Benchmark(Baseline = true)]
        public void AsyncManualResetEvent() { _amre.Set(); _amre.Reset(); }

        [Benchmark]
        public void AsyncExManualResetEvent() { _asyncEx.Set(); _asyncEx.Reset(); }

        [Benchmark]
        public void ManualResetEventSlim() { _mres.Set(); _mres.Reset(); }

        [Benchmark]
        public void ManualResetEvent() { _mre.Set(); _mre.Reset(); }
    }
}
