using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance.QueueTest
{
    [MaxIterationCount(20)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)]
    public class Queue1_1_CompareAll
    {
        private FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        private FairFeatureLockSubjects fairFeatureLockSubjects = new FairFeatureLockSubjects();
        private MonitorSubjects monitorSubjects = new MonitorSubjects();
        private SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        private ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        private AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        private AsyncExRWSubjects asyncExRwSubjects = new AsyncExRWSubjects();
        private NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        private FastSpinLockSubjects fastSpinLockSubjects = new FastSpinLockSubjects();
        private SpinLockSubjects spinLockSubjects = new SpinLockSubjects();
        private BmbsqdSubjects bmbsqdSubjects = new BmbsqdSubjects();
        private VSAsyncReaderWriterLockSubjects vSAsyncReaderWriterLockSubjects = new VSAsyncReaderWriterLockSubjects();
        private MicroValueLockSubjects microSpinLockSubjects = new MicroValueLockSubjects();

        private QueuePerformanceTest queueTest = new QueuePerformanceTest();

        [Params(1)]
        public int numProducers
        {
            set => queueTest.numProducers = value;
        }

        [Params(1)]
        public int numConsumers
        {
            set => queueTest.numConsumers = value;
        }

        [Params(100_000)]
        public int numMessages
        {
            set => queueTest.numOverallMessages = value;
        }

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => queueTest.Run(featureLockSubjects.Lock);

        /*[Benchmark]
        public void FairFeatureLock_Lock() => queueTest.Run(fairFeatureLockSubjects.Lock);

        [Benchmark]
        public void FeatureLock_LockPrio() => queueTest.Run(featureLockSubjects.LockPrio);
        
        [Benchmark]
        public void FastSpinLock_Lock() => queueTest.Run(fastSpinLockSubjects.Lock);
        */
        [Benchmark]
        public void MicroSpinLock_Lock() => queueTest.Run(microSpinLockSubjects.Lock);

        [Benchmark]
        public void SpinLock_Lock() => queueTest.Run(spinLockSubjects.Lock);

        [Benchmark]
        public void Monitor_Lock() => queueTest.Run(monitorSubjects.Lock);

        [Benchmark]
        public void SemaphoreSlim_Lock() => queueTest.Run(semaphoreSlimSubjects.Lock);

        [Benchmark]
        public void ReaderWriterLockSlim_Lock() => queueTest.Run(readerWriterLockSlimSubjects.Lock);

        [Benchmark]
        public void AsyncEx_Lock() => queueTest.Run(asyncExSubjects.Lock);

        [Benchmark]
        public void AsyncExRw_Lock() => queueTest.Run(asyncExRwSubjects.Lock);

        //[Benchmark]
        //public void NeoSmart_Lock() => queueTest.Run(neoSmartSubjects.Init, neoSmartSubjects.Lock);

        [Benchmark]
        public void FeatureLock_LockAsync_() => queueTest.AsyncRun(featureLockSubjects.LockAsync);

        /*[Benchmark]
        public void FairFeatureLock_LockAsync_() => queueTest.AsyncRun(fairFeatureLockSubjects.LockAsync);

        [Benchmark]
        public void FeatureLock_LockPrioAsync_() => queueTest.AsyncRun(featureLockSubjects.LockPrioAsync);
        */
        [Benchmark]
        public void SemaphoreSlim_LockAsync_() => queueTest.AsyncRun(semaphoreSlimSubjects.LockAsync);

        [Benchmark]
        public void AsyncEx_LockAsync_() => queueTest.AsyncRun(asyncExSubjects.LockAsync);

        [Benchmark]
        public void NeoSmart_LockAsync_() => queueTest.AsyncRun(neoSmartSubjects.LockAsync);

        [Benchmark]
        public void Bmbsqd_LockAsync_() => queueTest.AsyncRun(bmbsqdSubjects.LockAsync);

        [Benchmark]
        public void AsyncExRw_LockAsync_() => queueTest.AsyncRun(asyncExRwSubjects.LockAsync);

        [Benchmark]
        public void vSAsyncReaderWriter_LockAsync_() => queueTest.AsyncRun(vSAsyncReaderWriterLockSubjects.LockAsync);
/*
        [Benchmark]
        public void FeatureLock_ReentrantLock() => queueTest.Run(featureLockSubjects.ReentrantLock);

        [Benchmark]
        public void FeatureLock_ReentrantPrioLock() => queueTest.Run(featureLockSubjects.ReentrantPrioLock);

        [Benchmark]
        public void ReaderWriterLockSlim_ReentrantLock() => queueTest.Run(readerWriterLockSlimSubjects.ReentrantLock);

        [Benchmark]
        public void FeatureLock_ReentrantLockAsync_() => queueTest.AsyncRun(featureLockSubjects.ReentrantLockAsync);

        [Benchmark]
        public void FeatureLock_ReentrantPrioLockAsync_() => queueTest.AsyncRun(featureLockSubjects.ReentrantPrioLockAsync);
*/
    }
}