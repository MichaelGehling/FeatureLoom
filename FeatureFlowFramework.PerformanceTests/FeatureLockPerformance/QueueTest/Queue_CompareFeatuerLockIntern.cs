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
    public class Queue_CompareFeatuerLockIntern
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
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

        [Params(1_000_000)]
        public int numMessages
        {
            set => queueTest.numOverallMessages = value;
        }

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => queueTest.Run(featureLockSubjects.Init, featureLockSubjects.Lock);

        [Benchmark]
        public void FeatureLock_LockAsync() => queueTest.AsyncRun(featureLockSubjects.Init, featureLockSubjects.LockAsync);

    }
}
