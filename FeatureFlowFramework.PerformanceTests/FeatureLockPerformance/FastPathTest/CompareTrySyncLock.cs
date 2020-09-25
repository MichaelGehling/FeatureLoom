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
    public class FastPath_CompareTrySyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        MonitorSubjects monitorSubjects = new MonitorSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        SpinLockSubjects spinLockSubjects = new SpinLockSubjects();
        FastSpinLockSubjects fastSpinLockSubjects = new FastSpinLockSubjects();

        [Benchmark(Baseline = true)]
        public void FeatureLock_TryLock() => featureLockSubjects.TryLock();

        [Benchmark]
        public void FastSpinLock_TryLock() => fastSpinLockSubjects.TryLock();

        [Benchmark]
        public void Monitor_TryLock() => monitorSubjects.TryLock();

        [Benchmark]
        public void SemaphoreSlim_TryLock() => semaphoreSlimSubjects.TryLock();

        [Benchmark]
        public void ReaderWriterLockSlim_TryLock() => readerWriterLockSlimSubjects.TryLock();

        [Benchmark]
        public void SpinLock_TryLock() => spinLockSubjects.TryLock();
    }
}
