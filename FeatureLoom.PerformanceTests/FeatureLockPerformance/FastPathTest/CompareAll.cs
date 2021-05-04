using BenchmarkDotNet.Attributes;
using FeatureLoom.Helpers.Synchronization;

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
    public class FastPath_CompareAll
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
        public void MicroSpinLock_Lock_() => microSpinLockSubjects.Lock();

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => featureLockSubjects.Lock();

        [Benchmark]
        public void FastSpinLock_Lock() => fastSpinLockSubjects.Lock();

        [Benchmark]
        public void MicroSpinLock_Lock() => microSpinLockSubjects.Lock();

        [Benchmark]
        public void SpinLock_Lock() => spinLockSubjects.Lock();

        [Benchmark]
        public void Monitor_Lock() => monitorSubjects.Lock();

        [Benchmark]
        public void SemaphoreSlim_Lock() => semaphoreSlimSubjects.Lock();

        [Benchmark]
        public void ReaderWriterLockSlim_Lock() => readerWriterLockSlimSubjects.Lock();

        [Benchmark]
        public void AsyncEx_Lock() => asyncExSubjects.Lock();

        [Benchmark]
        public void AsyncExRW_Lock() => asyncExRWSubjects.Lock();

        //[Benchmark]
        //public void NeoSmart_Lock() => neoSmartSubjects.Lock();

        [Benchmark]
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

        [Benchmark]
        public void ReentrantFeatureLock_Lock() => featureLockSubjects.ReentrantLock();

        [Benchmark]
        public void ReentrantReaderWriterLockSlim_Lock() => readerWriterLockSlimSubjects.ReentrantLock();

        [Benchmark]
        public void ReentrantFeatureLock_LockAsync_() => featureLockSubjects.ReentrantLockAsync().WaitFor();
    }
}