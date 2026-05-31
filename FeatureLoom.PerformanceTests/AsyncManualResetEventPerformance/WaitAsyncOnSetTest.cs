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
    public class WaitAsyncOnSetTest
    {
        FeatureLoom.Synchronization.AsyncManualResetEvent _amre = new FeatureLoom.Synchronization.AsyncManualResetEvent(true);
        Nito.AsyncEx.AsyncManualResetEvent _asyncEx = new Nito.AsyncEx.AsyncManualResetEvent(true);
        ManualResetEventSlim _mres = new ManualResetEventSlim(true);
        ManualResetEvent _mre = new ManualResetEvent(true);

        [Benchmark(Baseline = true)]
        public void AsyncManualResetEvent() => _amre.WaitAsync().Wait();

        [Benchmark]
        public void AsyncExManualResetEvent() => _asyncEx.WaitAsync().Wait();

        [Benchmark]
        public void ManualResetEventSlim() => _mres.WaitHandle.WaitOneAsync(CancellationToken.None).Wait();

        [Benchmark]
        public void ManualResetEvent() => _mre.WaitOneAsync(CancellationToken.None).Wait();
    }
}
