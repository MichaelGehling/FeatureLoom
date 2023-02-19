using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public interface IStepExecutionController
    {
        Task ExecuteStepAsync<C>(C context, Step<C> step) where C : class, IStateMachineContext;

        void ExecuteStep<C>(C context, Step<C> step) where C : class, IStateMachineContext;
    }

    public class DefaultStepExecutionController : IStepExecutionController
    {
        public void ExecuteStep<C>(C context, Step<C> step) where C : class, IStateMachineContext
        {
            var currentExecutionState = context.CurrentExecutionState;
            var nextExecutionState = new Workflow.ExecutionState(currentExecutionState.stateIndex, currentExecutionState.stepIndex + 1);
            var nextExecutionPhase = Workflow.ExecutionPhase.Running;
            var proceedStep = true;
            PartialStep<C> partialStep = step;
            try
            {
                while (!CheckCondition(context, partialStep) && proceedStep)
                {
                    if (partialStep.doElse != null)
                    {
                        partialStep = partialStep.doElse;
                    }
                    else proceedStep = false;
                }

                if (proceedStep)
                {
                    if (partialStep.hasAction)
                    {
                        DoAction(context, partialStep);
                    }
                    else if (partialStep.hasWaiting)
                    {
                        proceedStep = DoWaiting(context, step, partialStep);
                    }

                    if (partialStep.targetStates != null || partialStep.finishStateMachine)
                    {
                        DoTransition(context, step, currentExecutionState, ref nextExecutionState, ref nextExecutionPhase, ref proceedStep, partialStep);
                    }
                }
            }
            catch (Exception e)
            {
                nextExecutionState = HandleException(context, step, currentExecutionState, nextExecutionState, e);
                proceedStep = false;
            }

            FinishStepExecution(context, nextExecutionState, nextExecutionPhase, proceedStep, partialStep);
        }

        public async Task ExecuteStepAsync<C>(C context, Step<C> step) where C : class, IStateMachineContext
        {
            var currentExecutionState = context.CurrentExecutionState;
            var nextExecutionState = new Workflow.ExecutionState(currentExecutionState.stateIndex, currentExecutionState.stepIndex + 1);
            var nextExecutionPhase = Workflow.ExecutionPhase.Running;
            var proceedStep = true;
            PartialStep<C> partialStep = step;
            try
            {
                while (!await CheckConditionAsync(context, partialStep) && proceedStep)
                {
                    if (partialStep.doElse != null)
                    {
                        partialStep = partialStep.doElse;
                    }
                    else proceedStep = false;
                }

                if (proceedStep)
                {
                    if (partialStep.hasAction)
                    {
                        await DoActionAsync(context, partialStep);
                    }
                    else if (partialStep.hasWaiting)
                    {
                        proceedStep = await DoWaitingAsync(context, step, proceedStep, partialStep);
                    }

                    if (partialStep.targetStates != null || partialStep.finishStateMachine)
                    {
                        DoTransition(context, step, currentExecutionState, ref nextExecutionState, ref nextExecutionPhase, ref proceedStep, partialStep);
                    }
                }
            }
            catch (Exception e)
            {
                nextExecutionState = await HandleExceptionAsync(context, step, currentExecutionState, nextExecutionState, e);
                proceedStep = false;
            }

            FinishStepExecution(context, nextExecutionState, nextExecutionPhase, proceedStep, partialStep);
        }

        private static void DoTransition<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, ref Workflow.ExecutionState nextExecutionState, ref Workflow.ExecutionPhase nextExecutionPhase, ref bool proceed, PartialStep<C> partialStep) where C : class, IStateMachineContext
        {
            if (partialStep.targetStates != null)
            {
                var targetState = partialStep.targetStates.Length == 1 ? partialStep.targetStates[0] : partialStep.targetStates[partialStep.targetStateIndex(context)];
                nextExecutionState = (targetState.stateIndex, 0);
                if (step.parentState != targetState) context.SendExecutionInfoEvent(Workflow.ExecutionEventList.StateTransition, targetState);
                proceed = false;
            }
            else if (partialStep.finishStateMachine)
            {
                nextExecutionState = (currentExecutionState.stateIndex, currentExecutionState.stepIndex);
                nextExecutionPhase = Workflow.ExecutionPhase.Finished;
                proceed = false;
            }
            else
            {
                Log.WARNING(context.GetHandle(), $"Workflow {context.ContextName} reaches state/step \"{step.parentState.Name}\"/\"{step.Description}\" without any defined action!");
            }
        }

        private static bool DoWaiting<C>(C context, Step<C> step, PartialStep<C> partialStep) where C : class, IStateMachineContext
        {
            bool proceed = true;
            context.ExecutionPhase = Workflow.ExecutionPhase.Waiting;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.BeginWaiting);
            try
            {
                partialStep.waitingDelegate?.Invoke(context);
            }
            catch (Exception e) when (e.InnerOrSelf() is TaskCanceledException)
            {
                proceed = false;
                Log.DEBUG(context.GetHandle(), "Waiting was cancelled!", e.InnerOrSelf().ToString());
            }
            context.ExecutionPhase = Workflow.ExecutionPhase.Running;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.EndWaiting);
            return proceed;
        }

        private static Workflow.ExecutionState HandleException<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : class, IStateMachineContext
        {
            if (!step.hasCatchException)
            {
                nextExecutionState = step.parentStateMachine.HandleException(context, e, nextExecutionState);
            }
            else
            {
                if (step.onException != null)
                {
                    HandleExceptionAction(context, step, e);
                }
                else if (step.onExceptionAsync != null)
                {
                    HandleAsyncExceptionAction(context, step, e);
                }
                else if (step.onExceptionTargetState != null)
                {
                    nextExecutionState = HandleExceptionTransition(context, step, e);
                }
                else if (step.onExceptionRepeatCondition != null)
                {
                    nextExecutionState = HandleExceptionRepeat(context, step, currentExecutionState, nextExecutionState, e);
                }
                else if (step.onExceptionRepeatConditionAsync != null)
                {
                    nextExecutionState = HandleAsyncExceptionRepeat(context, step, currentExecutionState, nextExecutionState, e);
                }
            }

            return nextExecutionState;
        }

        private static Workflow.ExecutionState HandleAsyncExceptionRepeat<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : class, IStateMachineContext
        {
            if (step.onExceptionRepeatConditionAsync(context, e).WaitFor())
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithRetry, e);
                nextExecutionState = currentExecutionState;
            }
            else context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithoutRetry, e);
            return nextExecutionState;
        }

        private static Workflow.ExecutionState HandleExceptionRepeat<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : class, IStateMachineContext
        {
            if (step.onExceptionRepeatCondition(context, e))
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithRetry, e);
                nextExecutionState = currentExecutionState;
            }
            else context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithoutRetry, e);
            return nextExecutionState;
        }

        private static void HandleAsyncExceptionAction<C>(C context, Step<C> step, Exception e) where C : class, IStateMachineContext
        {
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithAction, e);
            step.onExceptionAsync(context, e).WaitFor();
        }

        private static Workflow.ExecutionState HandleExceptionTransition<C>(C context, Step<C> step, Exception e) where C : class, IStateMachineContext
        {
            Workflow.ExecutionState nextExecutionState;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithTransition, e);
            nextExecutionState = (step.onExceptionTargetState.stateIndex, 0);
            return nextExecutionState;
        }

        private static void HandleExceptionAction<C>(C context, Step<C> step, Exception e) where C : class, IStateMachineContext
        {
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithAction, e);
            step.onException(context, e);
        }

        private static void DoAction<C>(C context, PartialStep<C> partialStep) where C : class, IStateMachineContext
        {
            if (partialStep.action != null) partialStep.action(context);
            else if (partialStep.actionAsync != null)
            {
                try
                {
                    partialStep.actionAsync(context).WaitFor();
                }
                catch (Exception e)
                {
                    if (e.InnerOrSelf() is TaskCanceledException) Log.DEBUG(context.GetHandle(), "Async action was cancelled!", e.InnerOrSelf().ToString());
                    else throw e.InnerOrSelf();
                }
            }
        }

        private static bool CheckCondition<C>(C context, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            bool result = true;
            if (partialStep.hasCondition)
            {
                if (partialStep.condition != null)
                {
                    if (!partialStep.condition(context)) result = false;
                }
                else if (partialStep.conditionAsync != null)
                {
                    if (!partialStep.conditionAsync(context).WaitFor()) result = false;
                }
            }

            return result;
        }

        private static void FinishStepExecution<C>(C context, Workflow.ExecutionState nextExecutionState, Workflow.ExecutionPhase nextPhase, bool proceed, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            if (partialStep.repeatWhileCondition && partialStep.hasCondition && proceed) { /* stay in execution state*/}
            else
            {
                context.CurrentExecutionState = nextExecutionState;
                context.ExecutionPhase = nextPhase;
            }

            if (context.PauseRequested)
            {
                context.ExecutionPhase = Workflow.ExecutionPhase.Paused;
                context.PauseRequested = false;
            }
        }

        private static async Task<bool> DoWaitingAsync<C>(C context, Step<C> step, bool proceed, PartialStep<C> partialStep) where C : class, IStateMachineContext
        {
            context.ExecutionPhase = Workflow.ExecutionPhase.Waiting;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.BeginWaiting);
            try
            {
                await partialStep.waitingAsyncDelegate?.Invoke(context);
            }
            catch (Exception e)
            {
                if (e.InnerOrSelf() is TaskCanceledException)
                {
                    proceed = false;
                    Log.DEBUG(context.GetHandle(), "Waiting was cancelled!", e.InnerOrSelf().ToString());
                }
                else throw;
            }
            context.ExecutionPhase = Workflow.ExecutionPhase.Running;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.EndWaiting);
            return proceed;
        }

        private static async Task<Workflow.ExecutionState> HandleExceptionAsync<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : class, IStateMachineContext
        {
            if (!step.hasCatchException)
            {
                nextExecutionState = step.parentStateMachine.HandleException(context, e, nextExecutionState);
            }
            else
            {
                if (step.onException != null)
                {
                    HandleExceptionAction(context, step, e);
                }
                else if (step.onExceptionAsync != null)
                {
                    await HandleAsyncExceptionActionAsync(context, step, e);
                }
                else if (step.onExceptionTargetState != null)
                {
                    nextExecutionState = HandleExceptionTransition(context, step, e);
                }
                else if (step.onExceptionRepeatCondition != null)
                {
                    nextExecutionState = HandleExceptionRepeat(context, step, currentExecutionState, nextExecutionState, e);
                }
                else if (step.onExceptionRepeatConditionAsync != null)
                {
                    nextExecutionState = HandleAsyncExceptionRepeatAsync(context, step, currentExecutionState, nextExecutionState, e);
                }
                else
                {
                    context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionCatched, e);
                }
            }

            return nextExecutionState;
        }

        private static Workflow.ExecutionState HandleAsyncExceptionRepeatAsync<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : class, IStateMachineContext
        {
            if (step.onExceptionRepeatConditionAsync(context, e).WaitFor())
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithRetry, e);
                nextExecutionState = currentExecutionState;
            }
            else context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithoutRetry, e);
            return nextExecutionState;
        }

        private static async Task HandleAsyncExceptionActionAsync<C>(C context, Step<C> step, Exception e) where C : class, IStateMachineContext
        {
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithAction, e);
            await step.onExceptionAsync(context, e);
        }

        private static async Task DoActionAsync<C>(C context, PartialStep<C> partialStep) where C : class, IStateMachineContext
        {
            if (partialStep.action != null) partialStep.action(context);
            else if (partialStep.actionAsync != null)
            {
                try
                {
                    await partialStep.actionAsync(context);
                }
                catch (Exception e)
                {
                    if (e.InnerOrSelf() is TaskCanceledException) Log.DEBUG(context.GetHandle(), "Async action was cancelled!", e.InnerOrSelf().ToString());
                    else throw e.InnerOrSelf();
                }
            }
        }

        private static async Task<bool> CheckConditionAsync<C>(C context, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            bool result = true;
            if (partialStep.hasCondition)
            {
                if (partialStep.condition != null)
                {
                    if (!partialStep.condition(context)) result = false;
                }
                else if (partialStep.conditionAsync != null)
                {
                    if (!await partialStep.conditionAsync(context)) result = false;
                }
            }

            return result;
        }
    }
}