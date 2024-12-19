using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance.QueueTest
{
    [MaxIterationCount(1001)]
    [MinIterationCount(1000)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)]
    public class QueueLockInTest
    {
        private FairFeatureLockSubjects fairFeatureLockSubjects = new FairFeatureLockSubjects();

        private QueuePerformanceTest queueTest = new QueuePerformanceTest();

        [Params(5)]
        public int numProducers
        {
            set => queueTest.numProducers = value;
        }

        [Params(5)]
        public int numConsumers
        {
            set => queueTest.numConsumers = value;
        }

        [Params(100_000)]
        public int numMessages
        {
            set => queueTest.numOverallMessages = value;
        }

        //[Benchmark]
        //public void FairFeatureLock_Lock() => queueTest.Run(fairFeatureLockSubjects.Lock);

    }
}