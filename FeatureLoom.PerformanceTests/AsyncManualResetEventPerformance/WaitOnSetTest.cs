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
    public class WaitOnSetTest
    {
        FeatureLoom.Synchronization.AsyncManualResetEvent _amre = new FeatureLoom.Synchronization.AsyncManualResetEvent(true);
        Nito.AsyncEx.AsyncManualResetEvent _asyncEx = new Nito.AsyncEx.AsyncManualResetEvent(true);
        ManualResetEventSlim _mres = new ManualResetEventSlim(true);
        ManualResetEvent _mre = new ManualResetEvent(true);

        [Benchmark(Baseline = true)]
        public void AsyncManualResetEvent() => _amre.Wait();

        [Benchmark]
        public void AsyncExManualResetEvent() => _asyncEx.Wait();

        [Benchmark]
        public void ManualResetEventSlim() => _mres.Wait();

        [Benchmark]
        public void ManualResetEvent() => _mre.WaitOne();
    }
}
