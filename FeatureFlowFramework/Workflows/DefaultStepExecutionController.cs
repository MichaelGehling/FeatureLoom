using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public interface IStepExecutionController
    {
        Task ExecuteStepAsync<C>(C context, Step<C> step) where C : IStateMachineContext;

        void ExecuteStep<C>(C context, Step<C> step) where C : IStateMachineContext;
    }

    public class DefaultStepExecutionController : IStepExecutionController
    {
        public void ExecuteStep<C>(C context, Step<C> step) where C : IStateMachineContext
        {
            var currentExecutionState = context.ExecutionState;
            var nextExecutionState = new WorkflowExecutionState(currentExecutionState.stateIndex, currentExecutionState.stepIndex + 1);
            var nextExecutionPhase = WorkflowExecutionPhase.Running;
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
                    else
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

        public async Task ExecuteStepAsync<C>(C context, Step<C> step) where C : IStateMachineContext
        {
            var currentExecutionState = context.ExecutionState;
            var nextExecutionState = new WorkflowExecutionState(currentExecutionState.stateIndex, currentExecutionState.stepIndex + 1);
            var nextExecutionPhase = WorkflowExecutionPhase.Running;
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
                    else
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

        private static void DoTransition<C>(C context, Step<C> step, WorkflowExecutionState currentExecutionState, ref WorkflowExecutionState nextExecutionState, ref WorkflowExecutionPhase nextExecutionPhase, ref bool proceed, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            if (partialStep.targetState != null)
            {
                nextExecutionState = new WorkflowExecutionState(partialStep.targetState.stateIndex, 0);
                if (step.parentStateMachine.logStateChanges && step.parentState != partialStep.targetState) Log.TRACE(context, $"Workflow {context.ContextName} changes state from \"{step.parentState.Name}\" to \"{partialStep.targetState.Name}\"");
                if (step.parentState != partialStep.targetState) context.SendExecutionInfoEvent(Workflow.ExecutionEventList.BeginWaiting);
                proceed = false;
            }
            else if (partialStep.finishStateMachine)
            {
                Log.TRACE(context, $"Workflow {context.ContextName} finishes execution in state/step \"{step.parentState.Name}\"/\"{step.Description}\"");
                nextExecutionState = new WorkflowExecutionState(currentExecutionState.stateIndex, currentExecutionState.stepIndex);
                nextExecutionPhase = WorkflowExecutionPhase.Finished;
                proceed = false;
            }
            else
            {
                Log.WARNING(context, $"Workflow {context.ContextName} reaches state/step \"{step.parentState.Name}\"/\"{step.Description}\" without any defined action!");
            }
        }

        private static bool DoWaiting<C>(C context, Step<C> step, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            bool proceed;
            if (step.parentStateMachine.logStartWaiting) Log.TRACE(context, $"Workflow {context.ContextName} starts waiting in state/step \"{step.parentState.Name}\"/\"{step.Description}\"");
            Task waitTask = partialStep.waitingTaskDelegate(context);
            var timeoutDelegate = partialStep.timeoutDelegate;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.BeginWaiting);

            context.Unlock();
            try
            {
                if (waitTask == null && timeoutDelegate != null) Task.Delay(timeoutDelegate(context).TotalMilliseconds.ToIntTruncated(), context.CancellationToken).Wait();
                else if (waitTask != null && timeoutDelegate != null) waitTask.Wait(timeoutDelegate(context).TotalMilliseconds.ToIntTruncated(), context.CancellationToken);
                else if (waitTask != null) waitTask.Wait(context.CancellationToken);
            }
            catch (Exception e)
            {
                if (e.InnerOrSelf() is TaskCanceledException) Log.DEBUG(context, "Waiting was cancelled!", e.InnerOrSelf().ToString());
                else throw e.InnerOrSelf();
            }
            context.TryLock(Timeout.InfiniteTimeSpan);

            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.EndWaiting);
            proceed = !waitTask.IsCanceled;
            if (step.parentStateMachine.logFinishWaiting) Log.TRACE(context, $"Workflow {context.ContextName} ends waiting in state/step \"{step.parentState.Name}\"/\"{step.Description}\"");
            return proceed;
        }

        private static WorkflowExecutionState HandleException<C>(C context, Step<C> step, WorkflowExecutionState currentExecutionState, WorkflowExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (!step.hasCatchException)
            {
                step.parentStateMachine.HandleException(context, e);
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
                    nextExecutionState = HandleExceptionTransition(context, step, currentExecutionState, nextExecutionState, e);
                }
                else if (step.onExceptionRepeatConditionAsync != null)
                {
                    nextExecutionState = HandleAsyncExceptionTransition(context, step, currentExecutionState, nextExecutionState, e);
                }
            }

            return nextExecutionState;
        }

        private static WorkflowExecutionState HandleAsyncExceptionTransition<C>(C context, Step<C> step, WorkflowExecutionState currentExecutionState, WorkflowExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (step.onExceptionRepeatConditionAsync(context, e).Result)
            {
                if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The step execution will be retried, because the defined condition was met!", e.ToString());
                nextExecutionState = currentExecutionState;
            }
            else if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The step execution will not be retried, because the defined condition was not met!", e.ToString());
            return nextExecutionState;
        }

        private static WorkflowExecutionState HandleExceptionTransition<C>(C context, Step<C> step, WorkflowExecutionState currentExecutionState, WorkflowExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (step.onExceptionRepeatCondition(context, e))
            {
                if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The step execution will be retried, because the defined condition was met!", e.ToString());
                nextExecutionState = currentExecutionState;
            }
            else if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The step execution will not be retried, because the defined condition was not met!", e.ToString());
            return nextExecutionState;
        }

        private static void HandleAsyncExceptionAction<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The defined action will be executed and then proceeded with the next step!", e.ToString());
            step.onExceptionAsync(context, e).Wait();
        }

        private static WorkflowExecutionState HandleExceptionTransition<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            WorkflowExecutionState nextExecutionState;
            if (step.parentStateMachine.logExeption || step.parentStateMachine.logStateChanges) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\" and will continue with state \"{step.onExceptionTargetState.Name}\"!", e.ToString());
            nextExecutionState = new WorkflowExecutionState(step.onExceptionTargetState.stateIndex, 0);
            return nextExecutionState;
        }

        private static void HandleExceptionAction<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The defined action will be executed and then proceeded with the next step!", e.ToString());
            step.onException(context, e);
        }

        private static void DoAction<C>(C context, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            if (partialStep.action != null) partialStep.action(context);
            else if (partialStep.actionAsync != null)
            {
                try
                {
                    partialStep.actionAsync(context).Wait();
                }
                catch (Exception e)
                {
                    if (e.InnerOrSelf() is TaskCanceledException) Log.DEBUG(context, "Async action was cancelled!", e.InnerOrSelf().ToString());
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
                    if (!partialStep.conditionAsync(context).Result) result = false;
                }
            }

            return result;
        }

        private static void FinishStepExecution<C>(C context, WorkflowExecutionState nextExecutionState, WorkflowExecutionPhase nextPhase, bool proceed, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            if (partialStep.repeatWhileCondition && partialStep.hasCondition && proceed) { /* stay in execution state*/}
            else
            {
                context.ExecutionState = nextExecutionState;
                context.ExecutionPhase = nextPhase;
            }

            if (context.PauseRequested)
            {
                context.ExecutionPhase = WorkflowExecutionPhase.Paused;
                context.PauseRequested = false;
            }
        }

        private static async Task<bool> DoWaitingAsync<C>(C context, Step<C> step, bool proceed, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            if (step.parentStateMachine.logStartWaiting) Log.TRACE(context, $"Workflow {context.ContextName} starts waiting in state/step \"{step.parentState.Name}\"/\"{step.Description}\"");
            Task waitTask = partialStep.waitingTaskDelegate(context);
            var timeoutDelegate = partialStep.timeoutDelegate;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.BeginWaiting);

            context.Unlock();
            try
            {
                if (waitTask == null && timeoutDelegate != null)
                {
                    await Task.Delay(timeoutDelegate(context).TotalMilliseconds.ToIntTruncated(), context.CancellationToken);
                    proceed = !context.CancellationToken.IsCancellationRequested;
                }
                else if (waitTask != null && timeoutDelegate != null) proceed = await waitTask.WaitAsync(timeoutDelegate(context), context.CancellationToken);
                else if (waitTask != null) proceed = await waitTask.WaitAsync(context.CancellationToken);
            }
            catch (Exception e)
            {
                if (e.InnerOrSelf() is TaskCanceledException) Log.DEBUG(context, "Waiting was cancelled!", e.InnerOrSelf().ToString());
                else throw e.InnerOrSelf();
            }
            await context.TryLockAsync(Timeout.InfiniteTimeSpan);

            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.EndWaiting);
            if (step.parentStateMachine.logFinishWaiting) Log.TRACE(context, $"Workflow {context.ContextName} ends waiting in state/step \"{step.parentState.Name}\"/\"{step.Description}\"");
            return proceed;
        }

        private static async Task<WorkflowExecutionState> HandleExceptionAsync<C>(C context, Step<C> step, WorkflowExecutionState currentExecutionState, WorkflowExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (!step.hasCatchException)
            {
                step.parentStateMachine.HandleException(context, e);
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
                    nextExecutionState = HandleExceptionTransition(context, step, currentExecutionState, nextExecutionState, e);
                }
                else if (step.onExceptionRepeatConditionAsync != null)
                {
                    nextExecutionState = HandleAsyncExceptionTransitionAsync(context, step, currentExecutionState, nextExecutionState, e);
                }
            }

            return nextExecutionState;
        }

        private static WorkflowExecutionState HandleAsyncExceptionTransitionAsync<C>(C context, Step<C> step, WorkflowExecutionState currentExecutionState, WorkflowExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (step.onExceptionRepeatConditionAsync(context, e).Result)
            {
                if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The step execution will be retried, because the defined condition was met!", e.ToString());
                nextExecutionState = currentExecutionState;
            }
            else if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The step execution will not be retried, because the defined condition was not met!", e.ToString());
            return nextExecutionState;
        }

        private static async Task HandleAsyncExceptionActionAsync<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            if (step.parentStateMachine.logExeption) Log.TRACE($"Workflow {context.ContextName} threw an exception in state/step \"{step.parentState.Name}\"/\"{step.Description}\". The defined action will be executed and then proceeded with the next step!", e.ToString());
            await step.onExceptionAsync(context, e);
        }

        private static async Task DoActionAsync<C>(C context, PartialStep<C> partialStep) where C : IStateMachineContext
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
                    if (e.InnerOrSelf() is TaskCanceledException) Log.DEBUG(context, "Async action was cancelled!", e.InnerOrSelf().ToString());
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