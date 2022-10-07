using FeatureLoom.Synchronization;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    public class ManualResetEventSubjects : IMreSubject
    {

        ManualResetEvent mre1 = new ManualResetEvent(false);
        ManualResetEvent mre2 = new ManualResetEvent(false);

        public Task Job1 { get; set; }
        public Task Job2 { get; set; }

        public void Set1() => mre1.Set();
        public void Reset1() => mre1.Reset();
        public void Wait1() => mre1.WaitOne();
        public Task WaitAsync1() => mre1.WaitOneAsync(CancellationToken.None);

        public void Set2() => mre2.Set();
        public void Reset2() => mre2.Reset();
        public void Wait2() => mre2.WaitOne();
        public Task WaitAsync2() => mre2.WaitOneAsync(CancellationToken.None);
    }
}
