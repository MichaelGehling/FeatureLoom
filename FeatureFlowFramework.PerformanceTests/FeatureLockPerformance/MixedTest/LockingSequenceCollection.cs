using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.MixedTest
{
    public class LockingSequenceCollection
    {
        List<LockingSequence> sequences = new List<LockingSequence>();
        LockingSequence hotPathSequence = null;
        int maxSteps;
        public int MaxSteps => maxSteps;

        public void SetHotPathSequence(LockingSequence hotPathSequence)
        {
            this.hotPathSequence = hotPathSequence;
            if (hotPathSequence.CountInLockSteps > maxSteps) maxSteps = hotPathSequence.CountInLockSteps;
            if (hotPathSequence.CountWaitingSteps > maxSteps) maxSteps = hotPathSequence.CountWaitingSteps;
        }

        public void AddSequence(LockingSequence sequence)
        {
            this.sequences.Add(sequence);
            if (sequence.CountInLockSteps > maxSteps) maxSteps = sequence.CountInLockSteps;
            if (sequence.CountWaitingSteps > maxSteps) maxSteps = sequence.CountWaitingSteps;
        }

        public void Run(Action<Action> lockAction, int numSteps, TimeSpan timeout)
        {
            List<Task> tasks = new List<Task>();
            IAsyncWaitHandle abortWaitHandle;
            if (hotPathSequence != null)
            {
                abortWaitHandle = hotPathSequence.WaitHandle;
                tasks.Add(hotPathSequence.RunAsync(numSteps, AsyncWaitHandle.NoWaitingHandle, lockAction));
            }

            foreach(var sequence in sequences)
            {
                tasks.Add(sequence.RunAsync(numSteps, AsyncWaitHandle.NoWaitingHandle, lockAction));
            }

            if (!Task.WhenAll(tasks.ToArray()).Wait(timeout)) Console.Write("TIMEOUT!");
        }

        public void Run(Func<Func<Task>, Task> lockAction, int numSteps, TimeSpan timeout)
        {
            List<Task> tasks = new List<Task>();
            IAsyncWaitHandle abortWaitHandle;
            if (hotPathSequence != null)
            {
                abortWaitHandle = hotPathSequence.WaitHandle;
                tasks.Add(hotPathSequence.RunAsync(numSteps, AsyncWaitHandle.NoWaitingHandle, lockAction));
            }

            foreach (var sequence in sequences)
            {
                tasks.Add(sequence.RunAsync(numSteps, AsyncWaitHandle.NoWaitingHandle, lockAction));
            }

            if (!Task.WhenAll(tasks.ToArray()).Wait(timeout)) Console.Write("TIMEOUT!");
        }
    }
}
