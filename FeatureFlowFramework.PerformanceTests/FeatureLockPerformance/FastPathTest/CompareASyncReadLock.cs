using BenchmarkDotNet.Attributes;
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
    public class FastPath_CompareAsyncReadLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        VSAsyncReaderWriterLockSubjects vSAsyncReaderWriterLockSubjects = new VSAsyncReaderWriterLockSubjects();
        AsyncExRWSubjects asyncExRWSubjects = new AsyncExRWSubjects();

        [Benchmark(Baseline = true)]
        public void FeatureLock_LockReadOnlyAsync_() => featureLockSubjects.LockReadOnlyAsync().WaitFor();

        [Benchmark]
        public void VSAsyncReaderWriterLock_LockReadOnlyAsync_() => vSAsyncReaderWriterLockSubjects.LockReadOnlyAsync().WaitFor();

        [Benchmark]
        public void AsyncExRW_LockReadOnlyAsync_() => asyncExRWSubjects.LockReadOnlyAsync().WaitFor();

    }
}
