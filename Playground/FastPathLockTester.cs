using System;
using System.Threading.Tasks;
using FeatureLoom.Helpers.Time;
using FeatureLoom.Helpers.Synchronization;

namespace Playground
{
    class FastPathLockTester<T>
    {
        string name;
        T lockObject;
        TimeSpan duration;
        Action<T> lockAction;
        FastPathLockTesterResult compareResult;

        public FastPathLockTester(string name, T lockObject, TimeSpan duration, FastPathLockTesterResult compareResult, Action<T> lockAction)
        {
            this.name = name;
            this.lockObject = lockObject;
            this.duration = duration;
            this.lockAction = lockAction;
            this.compareResult = compareResult;
        }

        public FastPathLockTesterResult Run()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            int gcc = GC.CollectionCount(0);

            long counter = 0;
            var timeFrame = new TimeFrame(duration);
            while(!timeFrame.Elapsed())
            {
                for(int i = 0; i < 100; i++)
                {
                    lockAction(lockObject);
                    counter++;
                }
            }
            gcc = GC.CollectionCount(0) - gcc;
            return new FastPathLockTesterResult(name, counter, compareResult, duration, gcc);
        }

    }

    class FastPathAsyncLockTester<T>
    {
        string name;
        T lockObject;
        TimeSpan duration;
        Func<T, Task> lockAction;
        FastPathLockTesterResult compareResult;

        public FastPathAsyncLockTester(string name, T lockObject, TimeSpan duration, FastPathLockTesterResult compareResult, Func<T, Task> lockAction)
        {
            this.name = name;
            this.lockObject = lockObject;
            this.duration = duration;
            this.lockAction = lockAction;
            this.compareResult = compareResult;
        }

        public FastPathLockTesterResult Run()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            int gcc = GC.CollectionCount(0);

            long counter = RunAsync().WaitFor();

            gcc = GC.CollectionCount(0) - gcc;
            return new FastPathLockTesterResult(name, counter, compareResult, duration, gcc);
        }

        private async Task<long> RunAsync()
        {
            long counter = 0;
            var timeFrame = new TimeFrame(duration);
            while(!timeFrame.Elapsed())
            {
                for(int i = 0; i < 100; i++)
                {
                    await lockAction(lockObject);
                    counter++;
                }
            }

            return counter;
        }
    }

    public class FastPathLockTesterResult
    {
        readonly public string name;
        readonly public long counter;
        readonly public FastPathLockTesterResult compareResult;
        readonly public TimeSpan duration;
        readonly public int gcc;

        public FastPathLockTesterResult(string name, long counter, FastPathLockTesterResult compareResult, TimeSpan duration, int gcc)
        {
            this.name = name;
            this.counter = counter;
            this.duration = duration;
            this.compareResult = compareResult;
            this.gcc = gcc;
        }

        public override string ToString()
        {
            double cps = counter / duration.TotalSeconds;
            double nsc = duration.TotalMilliseconds * 1_000_000 / counter;
            double cmp_nsc = 0;
            double gccmc = (double)gcc / counter * 1_000_000;
            if(compareResult != null) cmp_nsc = compareResult.duration.TotalMilliseconds * 1_000_000 / compareResult.counter;
            return $"{name}:\t Cycles:{counter},\t-> {cps} c/s / {nsc} ns per cycle / {nsc - cmp_nsc} ns per cycle (compared) / {gccmc} collection per 1mil cycles";
        }
    }
}
