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

                    if(partialStep.targetState != null || partialStep.finishStateMachine)
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
                    
                    if (partialStep.targetState != null || partialStep.finishStateMachine)
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

        private static void DoTransition<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, ref Workflow.ExecutionState nextExecutionState, ref Workflow.ExecutionPhase nextExecutionPhase, ref bool proceed, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            if (partialStep.targetState != null)
            {
                var targetState = partialStep.targetState(context);
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
                Log.WARNING(context, $"Workflow {context.ContextName} reaches state/step \"{step.parentState.Name}\"/\"{step.Description}\" without any defined action!");
            }
        }

        private static bool DoWaiting<C>(C context, Step<C> step, PartialStep<C> partialStep) where C : IStateMachineContext
        {
            bool proceed = true;
            //Task waitTask = partialStep.waitingTaskDelegate?.Invoke(context);
            //var timeoutDelegate = partialStep.timeoutDelegate;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.BeginWaiting);

            context.Unlock();
            try
            {
                /*if (waitTask == null && timeoutDelegate != null) Task.Delay(timeoutDelegate(context).TotalMilliseconds.ToIntTruncated(), context.CancellationToken).Wait();
                else if (waitTask != null && timeoutDelegate != null) waitTask.Wait(timeoutDelegate(context).TotalMilliseconds.ToIntTruncated(), context.CancellationToken);
                else if (waitTask != null) waitTask.Wait(context.CancellationToken);*/
                partialStep.waitingDelegate?.Invoke(context);
            }
            catch (Exception e)
            {
                if(e.InnerOrSelf() is TaskCanceledException)
                {
                    proceed = false;
                    Log.DEBUG(context, "Waiting was cancelled!", e.InnerOrSelf().ToString());
                }
                else throw e.InnerOrSelf();
            }
            context.TryLock(Timeout.InfiniteTimeSpan);

            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.EndWaiting);
            //proceed = !waitTask.IsCanceled;
            return proceed;
        }

        private static Workflow.ExecutionState HandleException<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
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

        private static Workflow.ExecutionState HandleAsyncExceptionRepeat<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (step.onExceptionRepeatConditionAsync(context, e).Result)
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithRetry, e);                
                nextExecutionState = currentExecutionState;
            }
            else context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithoutRetry, e);
            return nextExecutionState;
        }

        private static Workflow.ExecutionState HandleExceptionRepeat<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (step.onExceptionRepeatCondition(context, e))
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithRetry, e);
                nextExecutionState = currentExecutionState;
            }
            else context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithoutRetry, e);
            return nextExecutionState;
        }

        private static void HandleAsyncExceptionAction<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithAction, e);
            step.onExceptionAsync(context, e).Wait();
        }

        private static Workflow.ExecutionState HandleExceptionTransition<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            Workflow.ExecutionState nextExecutionState;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithTransition, e);
            nextExecutionState = (step.onExceptionTargetState.stateIndex, 0);
            return nextExecutionState;
        }

        private static void HandleExceptionAction<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithAction, e);
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

        private static async Task<bool> DoWaitingAsync<C>(C context, Step<C> step, bool proceed, PartialStep<C> partialStep) where C : IStateMachineContext
        {            
            //Task waitTask = partialStep.waitingTaskDelegate?.Invoke(context);
            //var timeoutDelegate = partialStep.timeoutDelegate;
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.BeginWaiting);

            
            context.Unlock();
            try
            {
                /*if (waitTask == null && timeoutDelegate != null)
                {
                    await Task.Delay(timeoutDelegate(context).TotalMilliseconds.ToIntTruncated(), context.CancellationToken);
                    proceed = !context.CancellationToken.IsCancellationRequested;
                }
                else if (waitTask != null && timeoutDelegate != null) proceed = await waitTask.WaitAsync(timeoutDelegate(context), context.CancellationToken);
                else if (waitTask != null) proceed = await waitTask.WaitAsync(context.CancellationToken);
                */
                await partialStep.waitingAsyncDelegate?.Invoke(context);
            }
            catch (Exception e)
            {
                if(e.InnerOrSelf() is TaskCanceledException)
                {
                    proceed = false;
                    Log.DEBUG(context, "Waiting was cancelled!", e.InnerOrSelf().ToString());
                }
                else throw e.InnerOrSelf();
            }
            await context.TryLockAsync(Timeout.InfiniteTimeSpan);

            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.EndWaiting);            
            return proceed;
        }

        private static async Task<Workflow.ExecutionState> HandleExceptionAsync<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
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

        private static Workflow.ExecutionState HandleAsyncExceptionRepeatAsync<C>(C context, Step<C> step, Workflow.ExecutionState currentExecutionState, Workflow.ExecutionState nextExecutionState, Exception e) where C : IStateMachineContext
        {
            if (step.onExceptionRepeatConditionAsync(context, e).Result)
            {
                context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithRetry, e);
                nextExecutionState = currentExecutionState;
            }
            else context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithoutRetry, e);
            return nextExecutionState;
        }

        private static async Task HandleAsyncExceptionActionAsync<C>(C context, Step<C> step, Exception e) where C : IStateMachineContext
        {
            context.SendExecutionInfoEvent(Workflow.ExecutionEventList.ExceptionWithAction, e);
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