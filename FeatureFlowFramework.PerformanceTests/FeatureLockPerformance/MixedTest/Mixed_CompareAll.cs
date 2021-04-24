using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.MixedTest
{
    [MaxIterationCount(20)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    public class Mixed_CompareAll
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

        MixedPerformanceTest test = new MixedPerformanceTest(5);

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => test.Run(featureLockSubjects.Lock);

        [Benchmark]
        public void Monitor_Lock() => test.Run(monitorSubjects.Lock);

        [Benchmark]
        public void FeatureLock_LockAsync_() => test.Run(featureLockSubjects.LockAsync);

        [Benchmark]
        public void SemaphoreSlim_LockAsync_() => test.Run(semaphoreSlimSubjects.LockAsync);

        
        

        [Benchmark]
        public void FeatureLock_LockPrio() => test.Run(featureLockSubjects.LockPrio);

        [Benchmark]
        public void FastSpinLock_Lock() => test.Run(fastSpinLockSubjects.Lock);          

        

        [Benchmark]
        public void SpinLock_Lock() => test.Run(spinLockSubjects.Lock);

        [Benchmark]
        public void SemaphoreSlim_Lock() => test.Run(semaphoreSlimSubjects.Lock);

        [Benchmark]
        public void RWLockSlim_Lock() => test.Run(readerWriterLockSlimSubjects.Lock);

        [Benchmark]
        public void AsyncEx_Lock() => test.Run(asyncExSubjects.Lock);

        [Benchmark]
        public void AsyncExRW_Lock() => test.Run(asyncExRwSubjects.Lock);

        [Benchmark]
        public void NeoSmart_Lock() => test.Run(neoSmartSubjects.Lock);   



        

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



    }
}
