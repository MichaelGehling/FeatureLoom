using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

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
    public class FastPath_CompareSyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        MonitorSubjects monitorSubjects = new MonitorSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        AsyncExRWSubjects asyncExRWSubjects = new AsyncExRWSubjects();
        ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        SpinLockSubjects spinLockSubjects = new SpinLockSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        FastSpinLockSubjects fastSpinLockSubjects = new FastSpinLockSubjects();

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => featureLockSubjects.Lock();

        [Benchmark]
        public void FastSpinLock_Lock() => fastSpinLockSubjects.Lock();

        [Benchmark]
        public void Monitor_Lock() => monitorSubjects.Lock();

        [Benchmark]
        public void SemaphoreSlim_Lock() => semaphoreSlimSubjects.Lock();

        [Benchmark]
        public void ReaderWriterLockSlim_Lock() => readerWriterLockSlimSubjects.Lock();

        [Benchmark]
        public void SpinLock_Lock() => spinLockSubjects.Lock();

        [Benchmark]
        public void AsyncEx_Lock() => asyncExSubjects.Lock();

        [Benchmark]
        public void AsyncExRW_Lock() => asyncExRWSubjects.Lock();

        [Benchmark]
        public void NeoSmart_Lock() => neoSmartSubjects.Lock();
    }
}
