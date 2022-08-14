using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    [MaxIterationCount(40)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    //[RPlotExporter]
    [HtmlExporter]
    public class SetResetTest
    {
        void RunTest(IMreSubject subject)
        {
            subject.Set1();
            subject.Reset1();
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
