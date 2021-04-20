using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPathTest
{
    [MaxIterationCount(40)]
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
        public void FeatureLock_LockAsync_() => featureLockSubjects.LockAsync().WaitFor();

        [Benchmark]
        public void SemaphoreSlim_LockAsync_() => semaphoreSlimSubjects.LockAsync().WaitFor();

        [Benchmark]
        public void VSAsyncReaderWriterLock_LockAsync_() => vSAsyncReaderWriterLockSubjects.LockAsync().WaitFor();

        [Benchmark]
        public void AsyncEx_LockAsync_() => asyncExSubjects.LockAsync().WaitFor();

        [Benchmark]
        public void AsyncExRW_LockAsync_() => asyncExRWSubjects.LockAsync().WaitFor();

        [Benchmark]
        public void Bmbsqd_LockAsync_() => bmbsqdSubjects.LockAsync().WaitFor();

        [Benchmark]
        public void NeoSmart_LockAsync_() => neoSmartSubjects.LockAsync().WaitFor();
    }
}
