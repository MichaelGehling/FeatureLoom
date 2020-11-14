using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.CongestionTest
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
    public class Congestion_CompareSyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        MonitorSubjects monitorSubjects = new MonitorSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        FastSpinLockSubjects fastSpinLockSubjects = new FastSpinLockSubjects();

        CongestionPerformanceTest congestionTest = new CongestionPerformanceTest();

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => congestionTest.Run(featureLockSubjects.Init, featureLockSubjects.Lock);

        //[Benchmark]
        public void FastSpinLock_Lock() => congestionTest.Run(fastSpinLockSubjects.Init, fastSpinLockSubjects.Lock);

        [Benchmark]
        public void FeatureLock_LockPrio() => congestionTest.Run(featureLockSubjects.Init, featureLockSubjects.LockPrio, featureLockSubjects.Lock);

        [Benchmark]
        public void Monitor_Lock() => congestionTest.Run(monitorSubjects.Init, monitorSubjects.Lock);

        //[Benchmark]
        public void SemaphoreSlim_Lock() => congestionTest.Run(semaphoreSlimSubjects.Init, semaphoreSlimSubjects.Lock);

        //[Benchmark]
        public void ReaderWriterLockSlim_Lock() => congestionTest.Run(readerWriterLockSlimSubjects.Init, readerWriterLockSlimSubjects.Lock);

        //[Benchmark]
        public void AsyncEx_Lock() => congestionTest.Run(asyncExSubjects.Init, asyncExSubjects.Lock);

        //[Benchmark]
        public void NeoSmart_Lock() => congestionTest.Run(neoSmartSubjects.Init, neoSmartSubjects.Lock);


    }
}
