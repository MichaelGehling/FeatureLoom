using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public abstract class StateMachine<CT> : StateMachine where CT : class, IStateMachineContext
    {
        protected readonly List<State<CT>> states = new List<State<CT>>();

        public override IStateInfo[] StateInfos => states.ToArray();
        public override IStateInfo StartStateInfo => states[InitialExecutionState.stateIndex];

        protected StateMachine()
        {
            Init();
            if (!ValidateAfterInit(out string findings))
            {
                findings = findings.TrimEnd("\n");
                Log.ERROR(this.GetHandle(), $"Creation of statemachine {this.GetType().FullName} failed!", $"Findings:\n{findings}");
                throw new Exception($"Creation of statemachine {this.GetType().FullName} failed! Findings: {findings}");
            }
            else if (!findings.EmptyOrNull())
            {
                findings = findings.TrimEnd("\n");
                Log.WARNING(this.GetHandle(), $"Issues found for Statemachine {this.GetType().FullName}!", $"Findings:\n{findings}");
            }
        }

        private bool ValidateAfterInit(out string findings)
        {
            findings = "";
            bool result = true;

            if (this.states.Count == 0)
            {
                findings += "CRITICAL: No states defined.\n";
                result = false;
            }

            if (InitialExecutionState.stateIndex + 1 > states.Count)
            {
                findings += "CRITICAL: No state at initial state index.\n";
                result = false;
            }

            foreach (var state in states)
            {
                findings = ValidateState(findings, state);
            }

            return result;
        }

        private static string ValidateState(string findings, State<CT> state)
        {
            if (state.steps.Count == 0)
            {
                state.steps.Add(new Step<CT>(state.parentStateMachine, state, state.steps.Count) { description = "Finish (automatically added)", finishStateMachine = true });
                findings += $"State {state.name} has no steps defined. A finish step was added.\n";
            }
            else
            {
                foreach (var step in state.steps)
                {
                    findings = ValidateStep(findings, state, step);
                }

                // Check if last step ends properly
                var lastStep = state.steps[state.steps.Count - 1];
                var validLastStep = false;
                PartialStep<CT> lastPartialStep = lastStep;
                while (lastPartialStep.doElse != null) lastPartialStep = lastPartialStep.doElse;
                if (!lastPartialStep.hasCondition && lastPartialStep.targetState != null) validLastStep = true;
                else if (!lastPartialStep.hasCondition && lastPartialStep.finishStateMachine) validLastStep = true;
                if (!validLastStep)
                {
                    state.steps.Add(new Step<CT>(state.parentStateMachine, state, state.steps.Count) { description = "Finish (automatically added)", finishStateMachine = true });
                    findings += $"State {state.name} has a last step {lastStep.description} without a final finish or goto. An extra finish step was added.\n";
                }
            }

            return findings;
        }

        private static string ValidateStep(string findings, State<CT> state, Step<CT> step)
        {
            if (step.description == null || step.description == "")
                findings += $"State {state.name} has a step at index {step.stepIndex} without description.\n";

            if (!(step.hasAction || step.hasWaiting || step.finishStateMachine || step.targetState != null))
                findings += $"State {state.name} has a step {step.description} at index {step.stepIndex} without any content. Remove or implement! \n";
            return findings;
        }

        public bool ExecuteNextStep(CT context, IStepExecutionController controller)
        {
            var executionState = context.CurrentExecutionState;
            var executionPhase = context.ExecutionPhase;

            if (!CheckAndUpdateExecutionStateBeforeExecution(context)) return false;

            var step = states.ItemOrNull(executionState.stateIndex)?.steps.ItemOrNull(executionState.stepIndex);
            if (step != null)
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.StepStarted);
                controller.ExecuteStep(context, step);
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.StepFinished, executionState, executionPhase);
                return EvaluteExecutionStateAfterExecution(context);
            }
            else
            {
                Log.ERROR(context.GetHandle(), $"StateMachine was called to execute, but the context's ({context.ContextName}) execution state refers to an invalid state or step index. Execution state is set to invalid!");
                context.ExecutionPhase = Workflow.ExecutionPhase.Invalid;
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.StepFailed, executionState, executionPhase);
                return false;
            }
        }

        public async Task<bool> ExecuteNextStepAsync(CT context, IStepExecutionController controller)
        {
            var executionState = context.CurrentExecutionState;
            var executionPhase = context.ExecutionPhase;

            if (!CheckAndUpdateExecutionStateBeforeExecution(context)) return false;

            var step = states.ItemOrNull(executionState.stateIndex)?.steps.ItemOrNull(executionState.stepIndex);
            if (step != null)
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.StepStarted);
                await controller.ExecuteStepAsync(context, step);
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.StepFinished, executionState, executionPhase);
                return EvaluteExecutionStateAfterExecution(context);
            }
            else
            {
                Log.ERROR(context.GetHandle(), $"StateMachine was called to execute, but the context's ({context.ContextName}) execution state refers to an invalid state or step index. Execution state is set to invalid!");
                context.ExecutionPhase = Workflow.ExecutionPhase.Invalid;
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.StepFailed, executionState, executionPhase);
                return false;
            }
        }

        private bool EvaluteExecutionStateAfterExecution(CT context)
        {
            switch (context.ExecutionPhase)
            {
                case Workflow.ExecutionPhase.Finished:
                    context.SendExecutionInfoEvent(Workflow.ExecutionEventList.WorkflowFinished);
                    return false;

                case Workflow.ExecutionPhase.Invalid:
                    Log.ERROR(context.GetHandle(), $"Workflow {context.ContextName} failed and is now invalid!");
                    context.SendExecutionInfoEvent(Workflow.ExecutionEventList.WorkflowInvalid);
                    return false;

                case Workflow.ExecutionPhase.Paused:
                    Log.INFO(context.GetHandle(), $"Workflow {context.ContextName} is paused!");
                    context.SendExecutionInfoEvent(Workflow.ExecutionEventList.WorkflowPaused);
                    return false;

                default:
                    return true;
            }
        }

        private static bool CheckAndUpdateExecutionStateBeforeExecution(CT context)
        {
            var executionPhase = context.ExecutionPhase;
            bool proceed = true;
            if (executionPhase == Workflow.ExecutionPhase.Finished ||
               executionPhase == Workflow.ExecutionPhase.Invalid)
            {
                Log.WARNING(context.GetHandle(), $"StateMachine was called to execute, but the context's ({context.ContextName}) execution state is in phase {executionPhase.ToString()}!");
                proceed = false;
            }
            else if (executionPhase != Workflow.ExecutionPhase.Running)
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.WorkflowStarted);
                context.ExecutionPhase = Workflow.ExecutionPhase.Running;
            }

            return proceed;
        }

        public override bool ExecuteNextStep<C>(C context, IStepExecutionController controller)
        {
            if (context is CT ct) return ExecuteNextStep(ct, controller);
            else
            {
                Log.ERROR(context.GetHandle(), $"Wrong context object used for this Statemachine! ({typeof(C)} instead of {typeof(CT)})");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<bool> ExecuteNextStepAsync<C>(C context, IStepExecutionController controller)
        {
            if (context is CT ct) return ExecuteNextStepAsync(ct, controller);
            else
            {
                Log.ERROR(context.GetHandle(), $"Wrong context object used for this Statemachine! ({typeof(C)} instead of {typeof(CT)})");
                return Task.FromResult(false);
            }
        }

        public virtual Workflow.ExecutionState HandleException(CT context, Exception e, Workflow.ExecutionState nextExecutionState)
        {
            var step = this.states.ItemOrNull(context.CurrentExecutionState.stateIndex)?.steps.ItemOrNull(context.CurrentExecutionState.stepIndex);
            if (this.robustExecutionActive)
            {
                Log.ERROR(context.GetHandle(), $"An exception was thrown, but the statemachine will continue to run! Problems might persist! ContextName={context.ContextName}, StateMachineName={Name}, StateName(Index)={step.parentState.name}({step.parentState.stateIndex}), StepIndex={step.stepIndex}.", e.ToString());
                return nextExecutionState;
            }
            else
            {
                Log.ERROR(context.GetHandle(), $"The state machine stopped, due to an unhandled exception! ContextName={context.ContextName}, StateMachineName={Name}, StateName(Index)={step.parentState.name}({step.parentState.stateIndex}), StepIndex={step.stepIndex}.", e.ToString());
                throw new Exception($"The state machine stopped, due to an unhandled exception! ContextName={context.ContextName}, StateMachineName={Name}, StateName(Index)={step.parentState.name}({step.parentState.stateIndex}), StepIndex={step.stepIndex}.", e);
            }
        }

        public State<CT> State(string name)
        {
            foreach (var state in states)
            {
                if (state.name == name) return state;
            }

            var index = states.Count;
            var newState = new State<CT>(this, index, name, "");
            states.Add(newState);
            return newState;
        }

        public IInitialStateBuilder<CT> BuildState(State<CT> state, string description = "")
        {
            state.steps.Clear();
            state.description = description;
            return new StateBuilder<CT>(state as State<CT>);
        }
    }

    public abstract class StateMachine : IStateMachineInfo
    {
        protected abstract void Init();

        private Workflow.ExecutionState initialExecutionState = (0, 0);
        protected bool robustExecutionActive = false;

        public Workflow.ExecutionState InitialExecutionState
        {
            get => initialExecutionState;
            protected set => initialExecutionState = value;
        }

        public string Name { get; }

        public abstract IStateInfo[] StateInfos { get; }
        public abstract IStateInfo StartStateInfo { get; }

        protected StateMachine()
        {
            Name = GetType().FullName;
        }

        public abstract Task<bool> ExecuteNextStepAsync<CT>(CT context, IStepExecutionController controller = null) where CT : class;

        public abstract bool ExecuteNextStep<CT>(CT context, IStepExecutionController controller = null) where CT : class;
    }
}