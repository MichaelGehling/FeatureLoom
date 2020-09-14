using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPathTest
{
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    public class FastPath_CompareReentrantAsyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();

        [Benchmark(Baseline = true)]
        public void ReentrantFeatureLock_LockAsync() => featureLockSubjects.ReentrantLockAsync().Wait();

        [Benchmark]
        public void ReentrantNeoSmart_LockAsync() => neoSmartSubjects.LockAsync().Wait();

    }
}
