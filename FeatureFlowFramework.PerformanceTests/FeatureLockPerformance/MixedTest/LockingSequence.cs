using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Helpers.Time;
using FeatureLoom.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance.MixedTest
{
    public class LockingSequence
    {
        private List<TimeSpan> inLockTimes = new List<TimeSpan>();
        private List<TimeSpan> waitingTimes = new List<TimeSpan>();
        private bool readOnly = false;
        private bool prioritized = false;
        private Action<Action> lockAction = null;
        private Func<Func<Task>, Task> lockActionAsync = null;
        private AsyncManualResetEvent waitHandle = new AsyncManualResetEvent(false);

        public bool IsReadOnly => readOnly;
        public bool IsPrioritized => prioritized;
        public IAsyncWaitHandle WaitHandle => waitHandle;
        public int CountInLockSteps => inLockTimes.Count;
        public int CountWaitingSteps => waitingTimes.Count;
        private int seed = -1;

        public LockingSequence SetReadOnly(bool readOnly)
        {
            this.readOnly = readOnly;
            return this;
        }

        public LockingSequence SetPrioritized(bool prioritized)
        {
            this.prioritized = prioritized;
            return this;
        }

        public LockingSequence AddInLockTime(TimeSpan time, int repetitions = 1)
        {
            for (int i = 0; i < repetitions; i++)
            {
                inLockTimes.Add(time);
            }
            return this;
        }

        public LockingSequence AddWaitingTime(TimeSpan time, int repetitions = 1)
        {
            for (int i = 0; i < repetitions; i++)
            {
                waitingTimes.Add(time);
            }
            return this;
        }

        public LockingSequence RandomizeSequences(int seed)
        {
            this.seed = seed;
            Random rnd = new Random(seed);

            List<TimeSpan> mixedInLockTimes = new List<TimeSpan>();
            while (inLockTimes.Count > 0)
            {
                int index = rnd.Next(inLockTimes.Count);
                mixedInLockTimes.Add(inLockTimes[index]);
                inLockTimes.RemoveAt(index);
            }

            List<TimeSpan> mixedWaitingTimes = new List<TimeSpan>();
            while (waitingTimes.Count > 0)
            {
                int index = rnd.Next(waitingTimes.Count);
                mixedWaitingTimes.Add(waitingTimes[index]);
                waitingTimes.RemoveAt(index);
            }

            inLockTimes = mixedInLockTimes;
            waitingTimes = mixedWaitingTimes;

            return this;
        }

        public LockingSequence SetLockAction(Action<Action> lockAction)
        {
            this.lockAction = lockAction;
            lockActionAsync = null;
            return this;
        }

        public LockingSequence SetLockAction(Func<Func<Task>, Task> lockActionAsync)
        {
            this.lockActionAsync = lockActionAsync;
            this.lockAction = null;
            return this;
        }

        public Task RunAsync(int numSteps, IAsyncWaitHandle abortWaitHandle)
        {
            this.waitHandle.Reset();

            if (lockAction != null)
            {
                var thread = new Thread(() => Execute(null, numSteps, abortWaitHandle));
                thread.Start();
                return waitHandle.WaitAsync();
            }
            else
            {
                return ExecuteAsync(null, numSteps, abortWaitHandle);
            }
        }

        public Task RunAsync(int numSteps, IAsyncWaitHandle abortWaitHandle, Action<Action> lockAction)
        {
            if (lockAction == null) return RunAsync(numSteps, abortWaitHandle);
            else
            {
                this.waitHandle.Reset();

                var thread = new Thread(() => Execute(lockAction, numSteps, abortWaitHandle));
                thread.Name = "Seq" + seed.ToString();
                thread.Start();
                return waitHandle.WaitAsync();
            }
        }

        public Task RunAsync(int numSteps, IAsyncWaitHandle abortWaitHandle, Func<Func<Task>, Task> lockActionAsync)
        {
            if (lockActionAsync == null) return RunAsync(numSteps, abortWaitHandle);
            else
            {
                this.waitHandle.Reset();

                return ExecuteAsync(lockActionAsync, numSteps, abortWaitHandle);
            }
        }

        protected void Execute(Action<Action> lockAction, int numSteps, IAsyncWaitHandle abortWaitHandle)
        {
            if (lockAction == null) lockAction = this.lockAction;

            for (int i = 0; i < numSteps; i++)
            {
                //if (!abortWaitHandle.WouldWait()) break;

                TimeSpan waitingTime = TimeSpan.Zero;
                if (waitingTimes.Count > 0)
                {
                    waitingTime = waitingTimes[i % waitingTimes.Count];
                }
                Wait(waitingTime, abortWaitHandle);

                //if (!abortWaitHandle.WouldWait()) break;

                TimeSpan inLockTime = TimeSpan.Zero;
                if (inLockTimes.Count > 0)
                {
                    inLockTime = inLockTimes[i % inLockTimes.Count];
                }
                lockAction(() => Wait(inLockTime, abortWaitHandle));

                //if (!abortWaitHandle.WouldWait()) break;
            }

            this.waitHandle.Set();
        }

        protected async Task ExecuteAsync(Func<Func<Task>, Task> lockActionAsync, int numSteps, IAsyncWaitHandle abortWaitHandle)
        {
            if (lockActionAsync == null) lockActionAsync = this.lockActionAsync;
            await Task.Yield();

            for (int i = 0; i < numSteps; i++)
            {
                //if (!abortWaitHandle.WouldWait()) break;

                TimeSpan waitingTime = TimeSpan.Zero;
                if (waitingTimes.Count > 0)
                {
                    waitingTime = waitingTimes[i % waitingTimes.Count];
                }
                await WaitAsync(waitingTime, abortWaitHandle);

                //if (!abortWaitHandle.WouldWait()) break;

                TimeSpan inLockTime = TimeSpan.Zero;
                if (inLockTimes.Count > 0)
                {
                    inLockTime = inLockTimes[i % inLockTimes.Count];
                }
                await lockActionAsync(async () => await WaitAsync(inLockTime, abortWaitHandle));

                //if (!abortWaitHandle.WouldWait()) break;
            }

            this.waitHandle.Set();
        }

        protected void Wait(TimeSpan time, IAsyncWaitHandle abortWaitHandle)
        {
            if (time == TimeSpan.Zero) return;
            Work(time); return;

            DateTime now = AppTime.Now;
            var timer = new TimeFrame(now, time);
            while (!timer.Elapsed(now))
            {
                //if (!abortWaitHandle.WouldWait()) return;
                if (timer.Remaining(now) < 0.002.Milliseconds()) /* do nothing */;
                else if (timer.Remaining(now) < 16.Milliseconds()) Thread.Sleep(0);
                else Thread.Sleep(timer.Remaining(now));

                now = AppTime.Now;
            }
        }

        private static void Work(TimeSpan time)
        {
            for (long i = 0; i < time.Ticks; i++)
            {
                // Simulating non CPU bound work (e.g. IO)
                if (i % 100 == 0) Thread.Sleep(0);
                else
                {
                    // simulating CPU bound work
                    for (int j = 0; j < 250; j++)
                    {
                        double x = Math.Sin((double)i * j);
                    }
                }
            }
        }

        protected async Task WaitAsync(TimeSpan time, IAsyncWaitHandle abortWaitHandle)
        {
            if (time == TimeSpan.Zero) return;
            Work(time); return;

            DateTime now = AppTime.Now;
            var timer = new TimeFrame(now, time);
            while (!timer.Elapsed(now))
            {
                //if (!abortWaitHandle.WouldWait()) return;
                if (timer.Remaining(now) < 0.002.Milliseconds()) /* do nothing */;
                else if (timer.Remaining(now) < 0.02.Milliseconds()) Thread.Sleep(0);
                else if (timer.Remaining(now) < 16.Milliseconds()) await Task.Yield();
                else await Task.Delay(timer.Remaining(now));

                now = AppTime.Now;
            }
        }
    }
}