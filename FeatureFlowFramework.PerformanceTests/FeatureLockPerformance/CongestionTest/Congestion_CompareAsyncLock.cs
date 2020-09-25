using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.CongestionTest
{
    [MaxIterationCount(20)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    public class Congestion_CompareAsyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();

        CongestionPerformanceTest congestionTest = new CongestionPerformanceTest();

        [Benchmark(Baseline = true)]
        public void FeatureLock_LockAsync_() => congestionTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.LockAsync);

        [Benchmark]
        public void FeatureLock_LockPrioAsync_() => congestionTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.LockPrioAsync, featureLockSubjects.LockAsync);

        [Benchmark]
        public void SemaphoreSlim_LockAsync_() => congestionTest.AsyncRun(semaphoreSlimSubjects.Init, semaphoreSlimSubjects.LockAsync);

        [Benchmark]
        public void AsyncEx_LockAsync_() => congestionTest.AsyncRun(asyncExSubjects.Init, asyncExSubjects.LockAsync);

        [Benchmark]
        public void NeoSmart_LockAsync_() => congestionTest.AsyncRun(neoSmartSubjects.Init, neoSmartSubjects.LockAsync);


    }
}
