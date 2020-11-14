using BenchmarkDotNet.Attributes;
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
    public class Queue_CompareAsyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        BmbsqdSubjects bmbsqdSubjects = new BmbsqdSubjects();

        QueuePerformanceTest queueTest = new QueuePerformanceTest();

        [Params(1, 10)]
        public int numProducers
        {
            set => queueTest.numProducers = value;
        }

        [Params(10)]
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
        public void FeatureLock_LockAsync_() => queueTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.LockAsync);

        [Benchmark]
        public void SemaphoreSlim_LockAsync_() => queueTest.AsyncRun(semaphoreSlimSubjects.Init, semaphoreSlimSubjects.LockAsync);

        //[Benchmark]
        public void AsyncEx_LockAsync_() => queueTest.AsyncRun(asyncExSubjects.Init, asyncExSubjects.LockAsync);

        //[Benchmark]
        public void NeoSmart_LockAsync_() => queueTest.AsyncRun(neoSmartSubjects.Init, neoSmartSubjects.LockAsync);

        //[Benchmark]
        public void Bmbsqd_LockAsync_() => queueTest.AsyncRun(bmbsqdSubjects.Init, bmbsqdSubjects.LockAsync);


    }
}
