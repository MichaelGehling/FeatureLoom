using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPath
{

    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    public class CompareFeatuerLockIntern
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => featureLockSubjects.Lock();

        [Benchmark]
        public void FeatureLock_LockReadOnly() => featureLockSubjects.LockReadOnly();

        [Benchmark]
        public void FeatureLock_LockAsync() => featureLockSubjects.LockAsync().Wait();

        [Benchmark]
        public void FeatureLock_LockReadOnlyAsync() => featureLockSubjects.LockReadOnlyAsync().Wait();

        [Benchmark]
        public void FeatureLock_TryLock() => featureLockSubjects.TryLock();

        [Benchmark]
        public void FeatureLock_TryLockReadOnly() => featureLockSubjects.TryLockReadOnly();

        [Benchmark]
        public void FeatureLock_ReentrantLock() => featureLockSubjects.ReentrantLock();

        [Benchmark]
        public void FeatureLock_ReentrantLockReadOnly() => featureLockSubjects.ReentrantLockReadOnly();

        [Benchmark]
        public void FeatureLock_ReentrantLockAsync() => featureLockSubjects.ReentrantLockAsync().Wait();

        [Benchmark]
        public void FeatureLock_ReentrantLockReadOnlyAsync() => featureLockSubjects.ReentrantLockReadOnlyAsync().Wait();

        [Benchmark]
        public void FeatureLock_ReentrantTryLock() => featureLockSubjects.ReentrantTryLock();

        [Benchmark]
        public void FeatureLock_ReentrantTryLockReadOnly() => featureLockSubjects.ReentrantTryLockReadOnly();
    }
}
