using FeatureLoom.Time;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance.MixedTest
{
    public class MixedPerformanceTest
    {
        private LockingSequenceCollection collection = new LockingSequenceCollection();

        public MixedPerformanceTest(int numThreads)
        {
            var x = numThreads - 1;
            for (int i = 0; i < numThreads; i++)
            {
                collection.AddSequence(new LockingSequence()
                    .AddInLockTime(10.0.Milliseconds(), 1)
                    .AddWaitingTime(x * 10.0.Milliseconds(), 1)
                    .AddInLockTime(1.0.Milliseconds(), 5)
                    .AddWaitingTime(x * 1.0.Milliseconds(), 5)
                    .AddInLockTime(0.1.Milliseconds(), 10)
                    .AddWaitingTime(x * 0.1.Milliseconds(), 10)
                    .AddInLockTime(0.01.Milliseconds(), 50)
                    .AddWaitingTime(x * 0.01.Milliseconds(), 50)
                    .AddInLockTime(0.001.Milliseconds(), 500)
                    .AddWaitingTime(x * 0.001.Milliseconds(), 500)
                    .AddInLockTime(0.0001.Milliseconds(), 5000)
                    .AddWaitingTime(x * 0.0001.Milliseconds(), 5000)
                    .AddInLockTime(0.Milliseconds(), 50_000)
                    .AddWaitingTime(0.Milliseconds(), 50_000)
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