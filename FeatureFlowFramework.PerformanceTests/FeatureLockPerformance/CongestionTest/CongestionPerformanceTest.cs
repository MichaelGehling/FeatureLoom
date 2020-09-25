using BenchmarkDotNet.Attributes;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.CongestionTest
{
    
    public class CongestionPerformanceTest
    {
        public int numCongestors = 10;
        public int numHotPathRuns = 1000;
        public TimeSpan hotPathExecutionTime = 0.01.Milliseconds();
        public TimeSpan congestionExecutionTime = 0.01.Milliseconds();

        public void Run(Action init, Action<Action> hotpathLock, Action<Action> congestingLock = null)
        {
            bool hotPathDone = false;

            init();

            if(congestingLock == null) congestingLock = hotpathLock;
            
            AsyncManualResetEvent starter = new AsyncManualResetEvent(false);            
            Task hotPathTask;
            List<Task> congesterTasks = new List<Task>();
           
            for(int i = 0; i < numCongestors; i++)
            {
                congesterTasks.Add(Task.Run(() =>
                {
                    starter.Wait();
                    while(!hotPathDone)
                    {
                        congestingLock(() =>
                        {
                            var timer = new TimeFrame(congestionExecutionTime);
                            while(!timer.Elapsed && !hotPathDone) /* work */;
                        });
                    }
                }));
            }

            hotPathTask = Task.Run(() =>
            {
                starter.Wait();
                int count = 0;
                while(count++ < numHotPathRuns)
                {
                    hotpathLock(() =>
                    {
                        var timer = new TimeFrame(hotPathExecutionTime);
                        while(!timer.Elapsed) /* work */;
                    });
                }
                hotPathDone = true;
            });

            starter.Set();

            if (!hotPathTask.Wait(10000)) Console.Write("! TIMEOUT !");
        }

        public void AsyncRun(Action init, Func<Action, Task> hotpathLock, Func<Action, Task> congestingLock = null)
        {
            bool hotPathDone = false;

            init();

            if(congestingLock == null) congestingLock = hotpathLock;

            AsyncManualResetEvent starter = new AsyncManualResetEvent(false);
            Task hotPathTask;
            List<Task> congesterTasks = new List<Task>();

            for(int i = 0; i < numCongestors; i++)
            {
                congesterTasks.Add(new Func<Task>(async () =>
                {
                    await starter.WaitAsync();
                    while(!hotPathDone)
                    {
                        await congestingLock(() =>
                        {
                            var timer = new TimeFrame(congestionExecutionTime);
                            while(!timer.Elapsed && !hotPathDone) /* work */;
                        });
                    }
                }).Invoke());
            }

            hotPathTask = Task.Run(async () =>
            {
                await starter.WaitAsync();
                int count = 0;
                while(count++ < numHotPathRuns)
                {
                    await hotpathLock(() =>
                    {
                        var timer = new TimeFrame(hotPathExecutionTime);
                        while(!timer.Elapsed) /* work */;
                    });
                }
                hotPathDone = true;
            });

            starter.Set();

            if(!hotPathTask.Wait(10000)) Console.Write("! TIMEOUT !");
        }
    }
}
