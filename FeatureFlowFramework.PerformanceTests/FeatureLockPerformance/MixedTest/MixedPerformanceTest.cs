using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers.Time;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.MixedTest
{
    public class MixedPerformanceTest
    {
        LockingSequenceCollection collection = new LockingSequenceCollection();    

        public MixedPerformanceTest(int numThreads)
        {
            for (int i = 0; i < numThreads; i++)
            {
                collection.AddSequence(new LockingSequence()
                    .AddInLockTime(0.1.Milliseconds(), 10)
                    .AddWaitingTime(0.2.Milliseconds(), 10)
                    .AddInLockTime(0.01.Milliseconds(), 50)
                    .AddWaitingTime(0.02.Milliseconds(), 50)
                    .AddInLockTime(0.001.Milliseconds(), 500)
                    .AddWaitingTime(0.002.Milliseconds(), 500)
                    .AddInLockTime(0.0001.Milliseconds(), 5000)
                    .AddWaitingTime(0.0002.Milliseconds(), 5000)
                    .AddInLockTime(0.Milliseconds(), 10_000)
                    .AddWaitingTime(0.Milliseconds(), 10_000)
                    .RandomizeSequences(i));
            }
        }

        public void Run(Func<Func<Task>, Task> lockAction)
        {
            collection.Run(lockAction, collection.MaxSteps, 10000.Seconds());
        }

        public void Run(Action<Action> lockAction)
        {
            collection.Run(lockAction, collection.MaxSteps, 10000.Seconds());
        }
    }
}
