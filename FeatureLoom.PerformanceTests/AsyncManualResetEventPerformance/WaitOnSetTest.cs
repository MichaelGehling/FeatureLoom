using BenchmarkDotNet.Attributes;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    [MaxIterationCount(40)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    //[RPlotExporter]
    [HtmlExporter]
    public class WaitOnSetTest
    {
        void RunTest(IMreSubject subject)
        {
            subject.Wait1();
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            asyncManualResetEventSubjects.Set1();
            asyncExManualResetEventSubjects.Set1();
            manualResetEventSlimSubjects.Set1();
            manualResetEventSubjects.Set1();
        }

        AsyncManualResetEventSubjects asyncManualResetEventSubjects = new AsyncManualResetEventSubjects();
        AsyncExManualResetEventSubjects asyncExManualResetEventSubjects = new AsyncExManualResetEventSubjects();
        ManualResetEventSlimSubjects manualResetEventSlimSubjects = new ManualResetEventSlimSubjects();
        ManualResetEventSubjects manualResetEventSubjects = new ManualResetEventSubjects();

        [Benchmark(Baseline = true)]
        public void AsyncManualResetEvent() => RunTest(asyncManualResetEventSubjects);

        [Benchmark]
        public void AsyncExManualResetEvent() => RunTest(asyncExManualResetEventSubjects);

        [Benchmark]
        public void ManualResetEventSlim() => RunTest(manualResetEventSlimSubjects);

        [Benchmark]
        public void ManualResetEvent() => RunTest(manualResetEventSubjects);
    }
}
