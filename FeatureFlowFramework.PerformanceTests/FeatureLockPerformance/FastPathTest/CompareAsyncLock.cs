using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
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
    public class FastPath_CompareAsyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        AsyncExRWSubjects asyncExRWSubjects = new AsyncExRWSubjects();
        BmbsqdSubjects bmbsqdSubjects = new BmbsqdSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        VSAsyncReaderWriterLockSubjects vSAsyncReaderWriterLockSubjects = new VSAsyncReaderWriterLockSubjects();

        [Benchmark(Baseline = true)]
        public void FeatureLock_LockAsync() => featureLockSubjects.LockAsync().Wait();

        [Benchmark]
        public void SemaphoreSlim_LockAsync() => semaphoreSlimSubjects.LockAsync().Wait();

        [Benchmark]
        public void VSAsyncReaderWriterLock_LockAsync() => vSAsyncReaderWriterLockSubjects.LockAsync().Wait();

        [Benchmark]
        public void AsyncEx_LockAsync() => asyncExSubjects.LockAsync().Wait();

        [Benchmark]
        public void AsyncExRW_LockAsync() => asyncExRWSubjects.LockAsync().Wait();

        [Benchmark]
        public void Bmbsqd_LockAsync() => bmbsqdSubjects.LockAsync().Wait();

        [Benchmark]
        public void NeoSmart_LockAsync() => neoSmartSubjects.LockAsync().Wait();
    }
}
