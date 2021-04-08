using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
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
    public class Queue_CompareAll
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        MonitorSubjects monitorSubjects = new MonitorSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        AsyncExRWSubjects asyncExRwSubjects = new AsyncExRWSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        FastSpinLockSubjects fastSpinLockSubjects = new FastSpinLockSubjects();
        SpinLockSubjects spinLockSubjects = new SpinLockSubjects();
        BmbsqdSubjects bmbsqdSubjects = new BmbsqdSubjects();
        VSAsyncReaderWriterLockSubjects vSAsyncReaderWriterLockSubjects = new VSAsyncReaderWriterLockSubjects();

        QueuePerformanceTest queueTest = new QueuePerformanceTest();

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
        public void FeatureLock_Lock() => queueTest.Run(featureLockSubjects.Init, featureLockSubjects.Lock);

        [Benchmark]
        public void FeatureLock_LockPrio() => queueTest.Run(featureLockSubjects.Init, featureLockSubjects.LockPrio);

        [Benchmark]
        public void FastSpinLock_Lock() => queueTest.Run(fastSpinLockSubjects.Init, fastSpinLockSubjects.Lock);

        [Benchmark]
        public void Monitor_Lock() => queueTest.Run(monitorSubjects.Init, monitorSubjects.Lock);

        [Benchmark]
        public void SemaphoreSlim_Lock() => queueTest.Run(semaphoreSlimSubjects.Init, semaphoreSlimSubjects.Lock);

        [Benchmark]
        public void ReaderWriterLockSlim_Lock() => queueTest.Run(readerWriterLockSlimSubjects.Init, readerWriterLockSlimSubjects.Lock);

        [Benchmark]
        public void SpinLock_Lock() => queueTest.Run(spinLockSubjects.Init, spinLockSubjects.Lock);

        [Benchmark]
        public void AsyncEx_Lock() => queueTest.Run(asyncExSubjects.Init, asyncExSubjects.Lock);

        [Benchmark]
        public void AsyncExRw_Lock() => queueTest.Run(null, asyncExRwSubjects.Lock);

        [Benchmark]
        public void NeoSmart_Lock() => queueTest.Run(neoSmartSubjects.Init, neoSmartSubjects.Lock);
        


        [Benchmark]
        public void FeatureLock_LockAsync_() => queueTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.LockAsync);

        [Benchmark]
        public void FeatureLock_LockPrioAsync_() => queueTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.LockPrioAsync);

        [Benchmark]
        public void SemaphoreSlim_LockAsync_() => queueTest.AsyncRun(semaphoreSlimSubjects.Init, semaphoreSlimSubjects.LockAsync);

        [Benchmark]
        public void AsyncEx_LockAsync_() => queueTest.AsyncRun(asyncExSubjects.Init, asyncExSubjects.LockAsync);

        [Benchmark]
        public void NeoSmart_LockAsync_() => queueTest.AsyncRun(neoSmartSubjects.Init, neoSmartSubjects.LockAsync);

        [Benchmark]
        public void Bmbsqd_LockAsync_() => queueTest.AsyncRun(bmbsqdSubjects.Init, bmbsqdSubjects.LockAsync);

        [Benchmark]
        public void AsyncExRw_LockAsync_() => queueTest.AsyncRun(null, asyncExRwSubjects.LockAsync);

        [Benchmark]
        public void vSAsyncReaderWriter_LockAsync_() => queueTest.AsyncRun(null, vSAsyncReaderWriterLockSubjects.LockAsync);



        [Benchmark]
        public void FeatureLock_ReentrantLock() => queueTest.Run(featureLockSubjects.Init, featureLockSubjects.ReentrantLock);

        [Benchmark]
        public void FeatureLock_ReentrantPrioLock() => queueTest.Run(featureLockSubjects.Init, featureLockSubjects.ReentrantPrioLock);

        [Benchmark]
        public void ReaderWriterLockSlim_ReentrantLock() => queueTest.Run(readerWriterLockSlimSubjects.Init, readerWriterLockSlimSubjects.ReentrantLock);



        [Benchmark]
        public void FeatureLock_ReentrantLockAsync_() => queueTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.ReentrantLockAsync);

        [Benchmark]
        public void FeatureLock_ReentrantPrioLockAsync_() => queueTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.ReentrantPrioLockAsync);
    }
}
