using BenchmarkDotNet.Attributes;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    [MaxIterationCount(100)]
    [MinIterationCount(80)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    //[RPlotExporter]
    [HtmlExporter]
    public class SetOnWaitingAsyncTest
    {
        void RunTest(IMreSubject subject)
        {
            subject.Set1();
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            asyncManualResetEventSubjects.Set1();
            asyncExManualResetEventSubjects.Set1();
            manualResetEventSlimSubjects.Set1();
            manualResetEventSubjects.Set1();
        }

        [IterationSetup(Target = nameof(AsyncManualResetEvent0))]
        public void ItSetup_AsyncManualResetEvent0() => Prepare(asyncManualResetEventSubjects);

        [IterationSetup(Target = nameof(AsyncManualResetEvent))]
        public void ItSetup_AsyncManualResetEvent() => Prepare(asyncManualResetEventSubjects);

        [IterationSetup(Target = nameof(AsyncExManualResetEvent))]
        public void ItSetup_AsyncExManualResetEvent() => Prepare(asyncExManualResetEventSubjects);

        [IterationSetup(Target = nameof(ManualResetEventSlim))]
        public void ItSetup_ManualResetEventSlim() => Prepare(manualResetEventSlimSubjects);

        [IterationSetup(Target = nameof(ManualResetEvent))]
        public void ItSetup_ManualResetEvent() => Prepare(manualResetEventSubjects);

        private static void Prepare(IMreSubject subject)
        {
            subject.Reset1();
            subject.Reset2();

            var job = Task.Run(async () =>
            {
                subject.Set2();
                await subject.WaitAsync1();                
            });

            subject.Wait2();
            //AppTime.WaitPrecisely(0.Milliseconds());
        }

        AsyncManualResetEventSubjects asyncManualResetEventSubjects = new AsyncManualResetEventSubjects();
        AsyncExManualResetEventSubjects asyncExManualResetEventSubjects = new AsyncExManualResetEventSubjects();
        ManualResetEventSlimSubjects manualResetEventSlimSubjects = new ManualResetEventSlimSubjects();
        ManualResetEventSubjects manualResetEventSubjects = new ManualResetEventSubjects();

        [Benchmark]
        public void AsyncManualResetEvent0() => RunTest(asyncManualResetEventSubjects);

        [Benchmark]
        public void AsyncExManualResetEvent() => RunTest(asyncExManualResetEventSubjects);

        [Benchmark]
        public void ManualResetEventSlim() => RunTest(manualResetEventSlimSubjects);

        [Benchmark]
        public void ManualResetEvent() => RunTest(manualResetEventSubjects);

        [Benchmark(Baseline = true)]
        public void AsyncManualResetEvent() => RunTest(asyncManualResetEventSubjects);
    }
}
