using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPath
{
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    public class CompareReentrantSyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        MonitorSubjects monitorSubjects = new MonitorSubjects();
        ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();

        [Benchmark(Baseline = true)]
        public void ReentrantFeatureLock_Lock() => featureLockSubjects.ReentrantLock();

        [Benchmark]
        public void ReentrantMonitor_Lock() => monitorSubjects.Lock();

        [Benchmark]
        public void ReentrantReaderWriterLockSlim_Lock() => readerWriterLockSlimSubjects.ReentrantLock();

        [Benchmark]
        public void ReentrantNeoSmart_Lock() => neoSmartSubjects.Lock();

    }
}
