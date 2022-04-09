using BenchmarkDotNet.Attributes;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance.MixedTest
{
    [MaxIterationCount(20)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [HtmlExporter]
    public class Mixed20_CompareAll
    {
        private FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        private MonitorSubjects monitorSubjects = new MonitorSubjects();
        private SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        private ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        private AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        private AsyncExRWSubjects asyncExRwSubjects = new AsyncExRWSubjects();
        private NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        private MicroValueLockSubjects microValueLockSubjects = new MicroValueLockSubjects();
        private SpinLockSubjects spinLockSubjects = new SpinLockSubjects();
        private BmbsqdSubjects bmbsqdSubjects = new BmbsqdSubjects();
        private VSAsyncReaderWriterLockSubjects vSAsyncReaderWriterLockSubjects = new VSAsyncReaderWriterLockSubjects();

        private MixedPerformanceTest test = new MixedPerformanceTest(20);


        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => test.Run(featureLockSubjects.Lock);

        [Benchmark]
        public void Monitor_Lock() => test.Run(monitorSubjects.Lock);

        [Benchmark]
        public void FeatureLock_LockAsync_() => test.Run(featureLockSubjects.LockAsync);

        [Benchmark]
        public void SemaphoreSlim_LockAsync_() => test.Run(semaphoreSlimSubjects.LockAsync);

        [Benchmark]
        public void MicroValueLock_Lock() => test.Run(microValueLockSubjects.Lock);

        [Benchmark]
        public void SpinLock_Lock() => test.Run(spinLockSubjects.Lock);
        /*
        [Benchmark]
        public void FeatureLock_LockPrio() => test.Run(featureLockSubjects.LockPrio);

        [Benchmark]
        public void SemaphoreSlim_Lock() => test.Run(semaphoreSlimSubjects.Lock);

        [Benchmark]
        public void RWLockSlim_Lock() => test.Run(readerWriterLockSlimSubjects.Lock);

        [Benchmark]
        public void AsyncEx_Lock() => test.Run(asyncExSubjects.Lock);

        [Benchmark]
        public void AsyncExRW_Lock() => test.Run(asyncExRwSubjects.Lock);

        //[Benchmark]
        //public void NeoSmart_Lock() => test.Run(neoSmartSubjects.Lock);

        [Benchmark]
        public void AsyncEx_LockAsync_() => test.Run(asyncExSubjects.LockAsync);

        [Benchmark]
        public void AsyncExRW_LockAsync_() => test.Run(asyncExRwSubjects.LockAsync);

        [Benchmark]
        public void NeoSmart_LockAsync_() => test.Run(neoSmartSubjects.LockAsync);

        [Benchmark]
        public void bmbsqd_LockAsync_() => test.Run(bmbsqdSubjects.LockAsync);

        [Benchmark]
        public void vSRWLock_LockAsync_() => test.Run(vSAsyncReaderWriterLockSubjects.LockAsync);

        [Benchmark]
        public void FeatureLock_LockReentrant() => test.Run(featureLockSubjects.ReentrantLock);

        [Benchmark]
        public void RWLockSlim_LockReentrant() => test.Run(readerWriterLockSlimSubjects.ReentrantLock);

        [Benchmark]
        public void FeatureLock_LockReentrantAsync_() => test.Run(featureLockSubjects.ReentrantLockAsync);
        */
    }
}