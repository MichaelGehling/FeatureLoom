using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Statemachines
{
    public sealed partial class Statemachine<T> where T : class
    {
        readonly Dictionary<string, State> _states = new Dictionary<string, State>();
        string _startStateName;
        public bool ForceAsyncRun { get; set; } = false;

        public Statemachine(State startState, params State[] otherStates)
        {
            if (startState.name.EmptyOrNull()) throw new ArgumentException("Each state must have a unique name!");            
            _startStateName = startState.name;
            _states[startState.name] = startState;
            foreach (var state in otherStates)
            {
                if (state.name.EmptyOrNull()) throw new ArgumentException("Each state must have a unique name!");
                if (_states.Keys.Contains(state.name)) throw new ArgumentException("Each state must have a unique name!");
                _states[state.name] = state;
            }
        }

        public Job CreateJob(T context)
        {
            Job job = new Job();
            job.CurrentStateName = _startStateName;
            job.Context = context;
            job.CancellationToken = CancellationToken.None;
            job.ExecutionState = ExecutionState.Created;
            return job;
        }

        public Job CreateAndStartJob(T context, CancellationToken cancellationToken = default)
        {
            Job job = CreateJob(context);
            StartJob(job, cancellationToken);
            return job;
        }

        public void StartJob(Job job, CancellationToken cancellationToken = default)
        {
            job.CurrentStateName = _startStateName;
            job.CancellationToken = cancellationToken;
            job.ExecutionTask = Run(job, false);
        }

        public void StartJob(IStatemachineJob job, CancellationToken cancellationToken = default)
        {
            if (!(job is Job typedJob)) throw new ArgumentException("Passed job is incompatible to statemachine.");
            StartJob(typedJob, cancellationToken);
        }

        public void ContinueJob(Job job, CancellationToken cancellationToken = default)
        {
            if (job.CurrentStateName.EmptyOrNull()) return;
            job.CancellationToken = cancellationToken;
            job.ExecutionTask = Run(job, false);
        }

        public void ContinueJob(IStatemachineJob job, CancellationToken cancellationToken = default)
        {
            if (!(job is Job typedJob)) throw new ArgumentException("Passed job is incompatible to statemachine.");
            ContinueJob(typedJob, cancellationToken);
        }

        public void ExecuteNextState(Job job, CancellationToken cancellationToken = default)
        {
            if (job.CurrentStateName.EmptyOrNull()) return;
            job.CancellationToken = cancellationToken;
            job.ExecutionTask = Run(job, true);
        }

        public void ExecuteNextState(IStatemachineJob job, CancellationToken cancellationToken = default)
        {
            if (!(job is Job typedJob)) throw new ArgumentException("Passed job is incompatible to statemachine.");
            ExecuteNextState(typedJob, cancellationToken);
        }

        private async Task Run(Job job, bool pauseAfterStateChange)
        {
            if (ForceAsyncRun) await Task.Yield();

            if (job.CancellationToken.IsCancellationRequested)
            {
                job.ExecutionState = ExecutionState.Interrupted;
                job.SendUpdate();
                return;
            }

            job.ExecutionState = ExecutionState.Executing;
            job.SendUpdate();

            while (job.ExecutionState == ExecutionState.Executing)
            {
                if (job.PauseRequested)
                {
                    job.ExecutionState = ExecutionState.Paused;
                    job.PauseRequested = false;
                }

                if (!_states.TryGetValue(job.CurrentStateName, out var state))
                {
                    Log.ERROR($"StateName {job.CurrentStateName} is invalid!");
                    throw new Exception($"StateName {job.CurrentStateName} is invalid!");
                }

                try
                {
                    job.CurrentStateName = await state.action(job.Context, job.CancellationToken);

                    if (job.CurrentStateName.EmptyOrNull()) job.ExecutionState = ExecutionState.Finished;
                    else if (job.CancellationToken.IsCancellationRequested) job.ExecutionState = ExecutionState.Interrupted;
                    else if (pauseAfterStateChange) job.PauseRequested = true;
                    job.SendUpdate();
                }
                catch(Exception e)
                {
                    job.ExecutionState = ExecutionState.Failed;
                    job.Exception = e;
                    job.SendUpdate();
                    Log.ERROR($"Execution of state {job.CurrentStateName} failed!", e.ToString());
                    return;
                }
            }
        }
    }
        
}
