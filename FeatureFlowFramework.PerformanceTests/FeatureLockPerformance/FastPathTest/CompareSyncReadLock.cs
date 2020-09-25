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
    public class FastPath_CompareSyncReadLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        AsyncExRWSubjects asyncExRWSubjects = new AsyncExRWSubjects();

        [Benchmark(Baseline = true)]
        public void FeatureLock_LockReadOnly() => featureLockSubjects.LockReadOnly();

        [Benchmark]
        public void ReaderWriterLockSlim_LockReadOnly() => readerWriterLockSlimSubjects.LockReadOnly();

        [Benchmark]
        public void AsyncExRW_LockReadOnly() => asyncExRWSubjects.LockReadOnly();

    }
}
