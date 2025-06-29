using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Statemachines
{
    /// <summary>
    /// An executable statemachine.
    /// The states for the statemachine are defined once via the constructor.
    /// Each state has a name and a function that takes the defined context object and optionally
    /// a cancellation token and returns the name of the state to transition to or an empty string to finish the job.
    /// It is possible to create many light-weight statemachine jobs that can be run
    /// independently from each other. 
    /// The statemachine jobs run asynchronously on the thread pool and can be paused or interruped
    /// via the cancellation token and can be continued later.
    /// Each state change is published via an IMessageSource as a hook to react from outside the 
    /// statemachine (e.g. for logging).
    /// </summary>
    /// <typeparam name="T">The type of the context object. Can be any reference type.</typeparam>
    public sealed partial class Statemachine<T> where T : class
    {
        readonly Dictionary<string, State> _states = new Dictionary<string, State>();
        string _startStateName;
        
        /// <summary>
        /// Even if all states run synchronous, at start the statemachine is forced to
        /// run on another thread-pool-thread. (Default is false)
        /// </summary>
        public bool ForceAsyncRun { get; set; } = false;

        /// <summary>
        /// An executable statemachine.
        /// The states for the statemachine are defined once via the constructor.
        /// Each state has a name and a function that takes the defined context object and optionally
        /// a cancellation token and returns the name of the state to transition to or an empty string to finish the job.
        /// It is possible to create many light-weight statemachine jobs that can be run
        /// independently from each other. 
        /// The statemachine jobs run asynchronously on the thread pool and can be paused or interruped
        /// via the cancellation token and can be continued later.
        /// Each state change is published via an IMessageSource as a hook to react from outside the 
        /// statemachine (e.g. for logging).
        /// </summary>
        /// <param name="startState">The first state to be executed on start</param>
        /// <param name="otherStates">All other states that can be reached via state transitions</param>
        /// <exception cref="ArgumentException"></exception>
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

        /// <summary>
        /// Creates a new statemachine job that can be started with StartJob()
        /// </summary>
        /// <param name="context">The context object, the statemachine works on.</param>
        /// <returns></returns>
        public Job CreateJob(T context)
        {
            Job job = new Job();
            job.CurrentStateName = _startStateName;
            job.Context = context;
            job.CancellationToken = CancellationToken.None;
            job.ExecutionState = ExecutionState.Created;
            return job;
        }

        /// <summary>
        /// Creates a new statemachine job and starts it right away from the start state
        /// </summary>
        /// <param name="context">The context object, the statemachine works on.</param>
        /// <param name="cancellationToken">Allows to interrupt the statemachine job.</param>
        /// <returns></returns>
        public Job CreateAndStartJob(T context, CancellationToken cancellationToken = default)
        {
            Job job = CreateJob(context);
            StartJob(job, cancellationToken);
            return job;
        }

        /// <summary>
        /// Starts the job from the start state.
        /// </summary>
        /// <param name="job">The job to be started.</param>
        /// <param name="cancellationToken">Allows to interrupt the statemachine job.</param>
        public void StartJob(Job job, CancellationToken cancellationToken = default)
        {
            job.CurrentStateName = _startStateName;
            job.CancellationToken = cancellationToken;
            job.ExecutionTask = Run(job, false);
        }

        /// <summary>
        /// Starts the job from the start state.
        /// </summary>
        /// <param name="job">The job to be started.</param>
        /// <param name="cancellationToken">Allows to interrupt the statemachine job.</param>
        public void StartJob(IStatemachineJob job, CancellationToken cancellationToken = default)
        {
            if (!(job is Job typedJob)) throw new ArgumentException("Passed job is incompatible to statemachine.");
            StartJob(typedJob, cancellationToken);
        }

        /// <summary>
        /// Continues a paused or interrupted job.
        /// </summary>
        /// <param name="job">The job to be continued.</param>
        /// <param name="cancellationToken">Allows to interrupt the statemachine job.</param>
        public void ContinueJob(Job job, CancellationToken cancellationToken = default)
        {
            if (job.CurrentStateName.EmptyOrNull()) return;
            job.CancellationToken = cancellationToken;
            job.ExecutionTask = Run(job, false);
        }

        /// <summary>
        /// Continues a paused or interrupted job.
        /// </summary>
        /// <param name="job">The job to be continued.</param>
        /// <param name="cancellationToken">Allows to interrupt the statemachine job.</param>
        public void ContinueJob(IStatemachineJob job, CancellationToken cancellationToken = default)
        {
            if (!(job is Job typedJob)) throw new ArgumentException("Passed job is incompatible to statemachine.");
            ContinueJob(typedJob, cancellationToken);
        }

        /// <summary>
        /// Executes only a single state and pauses in the state transition.
        /// </summary>
        /// <param name="job">The job to be executed for one state.</param>
        /// <param name="cancellationToken">Allows to interrupt the statemachine job.</param>
        public void ExecuteNextState(Job job, CancellationToken cancellationToken = default)
        {
            if (job.CurrentStateName.EmptyOrNull()) return;
            job.CancellationToken = cancellationToken;
            job.ExecutionTask = Run(job, true);
        }

        /// <summary>
        /// Executes only a single state and pauses in the state transition.
        /// </summary>
        /// <param name="job">The job to be executed for one state.</param>
        /// <param name="cancellationToken">Allows to interrupt the statemachine job.</param>
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
                    job.SendUpdate();
                    continue;
                }

                if (!_states.TryGetValue(job.CurrentStateName, out var state))
                {
                    OptLog.ERROR()?.Build($"StateName {job.CurrentStateName} is invalid!");
                    throw new Exception($"StateName {job.CurrentStateName} is invalid!");
                }

                try
                {
                    job.CurrentStateName = await state.action(job.Context, job.CancellationToken).ConfiguredAwait();

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
                    OptLog.ERROR()?.Build($"Execution of state {job.CurrentStateName} failed!", e);
                    return;
                }
            }
        }
    }
        
}
