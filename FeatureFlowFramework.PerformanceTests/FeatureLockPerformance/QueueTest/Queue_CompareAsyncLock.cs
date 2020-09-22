using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
{

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

        QueuePerformanceTest queueTest = new QueuePerformanceTest();

        [Params(1, 100)]
        public int numProducers
        {
            set => queueTest.numProducers = value;
        }

        [Params(1, 100)]
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
        public void FeatureLock_LockAsync() => queueTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.LockAsync);        

        [Benchmark]
        public void SemaphoreSlim_LockAsync() => queueTest.AsyncRun(semaphoreSlimSubjects.Init, semaphoreSlimSubjects.LockAsync);

        //[Benchmark]
        public void AsyncEx_LockAsync() => queueTest.AsyncRun(asyncExSubjects.Init, asyncExSubjects.LockAsync);

        //[Benchmark]
        public void NeoSmart_LockAsync() => queueTest.AsyncRun(neoSmartSubjects.Init, neoSmartSubjects.LockAsync);


    }
}
