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
    public class FastPath_CompareFeatuerLockIntern
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => featureLockSubjects.Lock();

        [Benchmark]
        public void FeatureLock_LockReadOnly() => featureLockSubjects.LockReadOnly();

        [Benchmark]
        public void FeatureLock_LockAsync_() => featureLockSubjects.LockAsync().Wait();

        [Benchmark]
        public void FeatureLock_LockReadOnlyAsync_() => featureLockSubjects.LockReadOnlyAsync().Wait();

        [Benchmark]
        public void FeatureLock_TryLock() => featureLockSubjects.TryLock();

        [Benchmark]
        public void FeatureLock_TryLockReadOnly() => featureLockSubjects.TryLockReadOnly();

        [Benchmark]
        public void FeatureLock_TryLockAsync_() => featureLockSubjects.TryLockAsync().Wait();

        [Benchmark]
        public void FeatureLock_TryLockReadOnlyAsync_() => featureLockSubjects.TryLockReadOnlyAsync().Wait();

        [Benchmark]
        public void FeatureLock_ReentrantLock() => featureLockSubjects.ReentrantLock();

        [Benchmark]
        public void FeatureLock_ReentrantLockReadOnly() => featureLockSubjects.ReentrantLockReadOnly();

        [Benchmark]
        public void FeatureLock_ReentrantLockAsync_() => featureLockSubjects.ReentrantLockAsync().Wait();

        [Benchmark]
        public void FeatureLock_ReentrantLockReadOnlyAsync_() => featureLockSubjects.ReentrantLockReadOnlyAsync().Wait();

        [Benchmark]
        public void FeatureLock_ReentrantTryLock() => featureLockSubjects.ReentrantTryLock();

        [Benchmark]
        public void FeatureLock_ReentrantTryLockReadOnly() => featureLockSubjects.ReentrantTryLockReadOnly();

        [Benchmark]
        public void FeatureLock_ReentrantTryLockAsync_() => featureLockSubjects.ReentrantTryLockAsync().Wait();

        [Benchmark]
        public void FeatureLock_ReentrantTryLockReadOnlyAsync_() => featureLockSubjects.ReentrantTryLockReadOnlyAsync().Wait();
    }
}
