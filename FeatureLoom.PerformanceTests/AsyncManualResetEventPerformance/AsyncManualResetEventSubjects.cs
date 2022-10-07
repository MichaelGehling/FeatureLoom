using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    public class AsyncManualResetEventSubjects : IMreSubject
    {

        AsyncManualResetEvent mre1 = new AsyncManualResetEvent(false);
        AsyncManualResetEvent mre2 = new AsyncManualResetEvent(false);

        public AsyncManualResetEventSubjects()
        {
            //mre1.YieldTimesBeforeSyncSleep = 0;
            //mre2.YieldTimesBeforeSyncSleep = 0;
            //mre1.YieldTimesBeforeAsyncSleep = 0;
        }
        

        public Task Job1 { get; set; }
        public Task Job2 { get; set; }

        public void Set1() => mre1.Set();
        public void Reset1() => mre1.Reset();
        public void Wait1() => mre1.Wait();
        public Task WaitAsync1() => mre1.WaitAsync();

        public void Set2() => mre2.Set();
        public void Reset2() => mre2.Reset();
        public void Wait2() => mre2.Wait();
        public Task WaitAsync2() => mre2.WaitAsync();
    }
}
