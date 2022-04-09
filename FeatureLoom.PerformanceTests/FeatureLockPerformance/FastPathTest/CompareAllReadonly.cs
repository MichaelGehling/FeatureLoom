using BenchmarkDotNet.Attributes;
using FeatureLoom.Synchronization;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance.FastPathTest
{
    [MaxIterationCount(40)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    public class FastPath_CompareAllReadOnly
    {
        private FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        private MonitorSubjects monitorSubjects = new MonitorSubjects();
        private SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        private AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        private AsyncExRWSubjects asyncExRWSubjects = new AsyncExRWSubjects();
        private ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        private SpinLockSubjects spinLockSubjects = new SpinLockSubjects();
        private NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        private FastSpinLockSubjects fastSpinLockSubjects = new FastSpinLockSubjects();
        private BmbsqdSubjects bmbsqdSubjects = new BmbsqdSubjects();
        private VSAsyncReaderWriterLockSubjects vSAsyncReaderWriterLockSubjects = new VSAsyncReaderWriterLockSubjects();
        private MicroValueLockSubjects microSpinLockSubjects = new MicroValueLockSubjects();

        [Benchmark]
        public void MicroSpinLock_Lock_() => microSpinLockSubjects.LockReadOnly();

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => featureLockSubjects.LockReadOnly();

        [Benchmark]
        public void FastSpinLock_Lock() => fastSpinLockSubjects.LockReadOnly();

        [Benchmark]
        public void MicroSpinLock_Lock() => microSpinLockSubjects.LockReadOnly();

        [Benchmark]
        public void ReaderWriterLockSlim_Lock() => readerWriterLockSlimSubjects.LockReadOnly();

        [Benchmark]
        public void AsyncExRW_Lock() => asyncExRWSubjects.LockReadOnly();

        [Benchmark]
        public void FeatureLock_LockAsync_() => featureLockSubjects.LockReadOnlyAsync().WaitFor();

        [Benchmark]
        public void VSAsyncReaderWriterLock_LockAsync_() => vSAsyncReaderWriterLockSubjects.LockReadOnlyAsync().WaitFor();

        [Benchmark]
        public void AsyncExRW_LockAsync_() => asyncExRWSubjects.LockReadOnlyAsync().WaitFor();
    }
}