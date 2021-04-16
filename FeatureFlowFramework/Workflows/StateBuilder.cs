using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class StateBuilder<CT> : IAfterStartStateBuilder<CT>, INextStateBuilder<CT>, IInitialStateBuilder<CT>,
                                    IAfterPreconditionStateBuilder<CT>, IAfterActionStateBuilder<CT>,
                                    IAfterWhileStateBuilder<CT>, IAfterConditionedActionStateBuilder<CT>,
                                    IAfterFinalPreconditionStateBuilder<CT>, IAfterActionStateBuilderWithoutTransition<CT>, 
                                    IAfterConditionedStateBuilderWithoutTransition<CT>
                                    where CT : class, IStateMachineContext
    {
        public State<CT> state;

        public StateBuilder(State<CT> state)
        {
            this.state = state;
        }

        private Step<CT> CurrentStep => state.steps[state.steps.Count - 1];

        private PartialStep<CT> CurrentPartialStep
        {
            get
            {
                PartialStep<CT> partial = CurrentStep;
                while (partial.doElse != null) partial = partial.doElse;
                return partial;
            }
        }

        private Step<CT> NewStep
        {
            get
            {
                state.steps.Add(new Step<CT>(state.parentStateMachine, state, state.steps.Count));
                return CurrentStep;
            }
        }

        public INextStateBuilder<CT> Catch()
        {
            CurrentStep.hasCatchException = true;
            return this;
        }

        public INextStateBuilder<CT> CatchAndDo(Func<CT, Exception, Task> action)
        {
            CurrentStep.hasCatchException = true;
            CurrentStep.onExceptionAsync = action;
            return this;
        }

        public INextStateBuilder<CT> CatchAndDo(Action<CT, Exception> action)
        {
            CurrentStep.hasCatchException = true;
            CurrentStep.onException = action;
            return this;
        }

        public INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, Task<bool>> predicate)
        {
            CurrentStep.hasCatchException = true;
            CurrentStep.onExceptionRepeatConditionAsync = predicate;
            return this;
        }

        public INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, bool> predicate)
        {
            CurrentStep.hasCatchException = true;
            CurrentStep.onExceptionRepeatCondition = predicate;
            return this;
        }

        public IAfterActionStateBuilder<CT> Do(Func<CT, Task> action)
        {
            CurrentPartialStep.hasAction = true;
            CurrentPartialStep.actionAsync = action;
            return this;
        }

        public IAfterActionStateBuilder<CT> Do(Action<CT> action)
        {
            CurrentPartialStep.hasAction = true;
            CurrentPartialStep.action = action;
            return this;
        }

        public State<CT> Finish()
        {
            CurrentPartialStep.finishStateMachine = true;
            return state;
        }

        public State<CT> Goto(State targetState)
        {
            CurrentPartialStep.targetState = c => targetState;
            return this.state;
        }

        public State<CT> Goto(Func<CT, State> targetState)
        {
            CurrentPartialStep.targetState = targetState;
            return this.state;
        }

        public IAfterPreconditionStateBuilder<CT> If(Func<CT, Task<bool>> predicate)
        {
            CurrentPartialStep.hasCondition = true;
            CurrentPartialStep.conditionAsync = predicate;
            return this;
        }

        public IAfterPreconditionStateBuilder<CT> If(Func<CT, bool> predicate)
        {
            CurrentPartialStep.hasCondition = true;
            CurrentPartialStep.condition = predicate;
            return this;
        }

        public IAfterPreconditionStateBuilder<CT> ElseIf(Func<CT, Task<bool>> predicate)
        {
            CurrentPartialStep.doElse = new PartialStep<CT>();
            CurrentPartialStep.hasCondition = true;
            CurrentPartialStep.conditionAsync = predicate;
            return this;
        }

        public IAfterPreconditionStateBuilder<CT> ElseIf(Func<CT, bool> predicate)
        {
            CurrentPartialStep.doElse = new PartialStep<CT>();
            CurrentPartialStep.hasCondition = true;
            CurrentPartialStep.condition = predicate;
            return this;
        }

        public IAfterFinalPreconditionStateBuilder<CT> Else()
        {
            CurrentPartialStep.doElse = new PartialStep<CT>();
            return this;
        }

        public State<CT> Loop()
        {
            CurrentPartialStep.targetState = c => state;
            return state;
        }

        public IAfterStartStateBuilder<CT> Step(string description = "")
        {
            NewStep.description = description;
            return this;
        }

        public IAfterActionStateBuilder<CT> Using(Func<CT, object> resource)
        {
            CurrentStep.AddUsingResource(resource);
            return this;
        }

        public IAfterActionStateBuilder<CT> Wait(Func<CT, TimeSpan> waitingTime)
        {
            CurrentPartialStep.hasWaiting = true;
            CurrentPartialStep.waitingDelegate = c => c.CancellationToken.WaitHandle.WaitOne(waitingTime(c));
            CurrentPartialStep.waitingAsyncDelegate = c => Task.Delay(waitingTime(c), c.CancellationToken);
            return this;
        }

        public IAfterActionStateBuilder<CT> Wait(TimeSpan waitingTime)
        {
            CurrentPartialStep.hasWaiting = true;
            CurrentPartialStep.waitingDelegate = c => c.CancellationToken.WaitHandle.WaitOne(waitingTime);
            CurrentPartialStep.waitingAsyncDelegate = c => Task.Delay(waitingTime, c.CancellationToken);
            return this;
        }

        public IAfterActionStateBuilder<CT> WaitFor(Func<CT, IAsyncWaitHandle> waitHandle, Func<CT, TimeSpan> timeout = default)
        {
            CurrentPartialStep.hasWaiting = true;
            if(timeout == default)            
            {
                CurrentPartialStep.waitingDelegate = c => waitHandle(c).Wait(c.CancellationToken);
                CurrentPartialStep.waitingAsyncDelegate = c => waitHandle(c).WaitAsync(c.CancellationToken);
            }
            else
            {
                CurrentPartialStep.waitingDelegate = c => waitHandle(c).Wait(timeout(c), c.CancellationToken);
                CurrentPartialStep.waitingAsyncDelegate = c => waitHandle(c).WaitAsync(timeout(c), c.CancellationToken);
            }
            return this;
        }

        public IAfterActionStateBuilder<CT> WaitForAll(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default)
        {
            CurrentPartialStep.hasWaiting = true;
            if(timeout == default)
            {
                CurrentPartialStep.waitingDelegate = c => AsyncWaitHandle.WaitAll(c.CancellationToken, waitHandles(c));
                CurrentPartialStep.waitingAsyncDelegate = c => AsyncWaitHandle.WaitAllAsync(c.CancellationToken, waitHandles(c));
            }
            else
            {
                CurrentPartialStep.waitingDelegate = c => AsyncWaitHandle.WaitAll(timeout(c), c.CancellationToken, waitHandles(c));
                CurrentPartialStep.waitingAsyncDelegate = c => AsyncWaitHandle.WaitAllAsync(timeout(c), c.CancellationToken, waitHandles(c));
            }
            return this;
        }

        public IAfterActionStateBuilder<CT> WaitForAny(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default)
        {
            CurrentPartialStep.hasWaiting = true;
            if(timeout == default)
            {
                CurrentPartialStep.waitingDelegate = c => AsyncWaitHandle.WaitAny(c.CancellationToken, waitHandles(c));
                CurrentPartialStep.waitingAsyncDelegate = c => AsyncWaitHandle.WaitAnyAsync(c.CancellationToken, waitHandles(c));
            }
            else
            {
                CurrentPartialStep.waitingDelegate = c => AsyncWaitHandle.WaitAny(timeout(c), c.CancellationToken, waitHandles(c));
                CurrentPartialStep.waitingAsyncDelegate = c => AsyncWaitHandle.WaitAnyAsync(timeout(c), c.CancellationToken, waitHandles(c));
            }
            return this;
        }

        public IAfterWhileStateBuilder<CT> While(Func<CT, Task<bool>> predicate)
        {
            CurrentPartialStep.hasCondition = true;
            CurrentPartialStep.repeatWhileCondition = true;
            CurrentPartialStep.conditionAsync = predicate;
            return this;
        }

        public IAfterWhileStateBuilder<CT> While(Func<CT, bool> predicate)
        {
            CurrentPartialStep.hasCondition = true;
            CurrentPartialStep.repeatWhileCondition = true;
            CurrentPartialStep.condition = predicate;
            return this;
        }

        public INextStateBuilder<CT> CatchAndGoto(State targetState)
        {
            CurrentStep.hasCatchException = true;
            CurrentStep.onExceptionTargetState = targetState;
            return this;
        }

        IAfterPreconditionStateBuilder<CT> IAfterStartStateBuilder<CT>.If(Func<CT, Task<bool>> predicate)
        {
            this.If(predicate);
            return this;
        }

        IAfterPreconditionStateBuilder<CT> IAfterStartStateBuilder<CT>.If(Func<CT, bool> predicate)
        {
            this.If(predicate);
            return this;
        }

        IAfterConditionedActionStateBuilder<CT> IAfterPreconditionStateBuilder<CT>.Do(Func<CT, Task> action)
        {
            this.Do(action);
            return this;
        }

        IAfterConditionedActionStateBuilder<CT> IAfterPreconditionStateBuilder<CT>.Do(Action<CT> action)
        {
            this.Do(action);
            return this;
        }


        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterPreconditionStateBuilder<CT>.Goto(State targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterPreconditionStateBuilder<CT>.Loop()
        {
            this.Loop();
            return this;
        }

        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterPreconditionStateBuilder<CT>.Finish()
        {
            this.Finish();
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterFinalPreconditionStateBuilder<CT>.Goto(State targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterFinalPreconditionStateBuilder<CT>.Loop()
        {
            this.Loop();
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterFinalPreconditionStateBuilder<CT>.Finish()
        {
            this.Finish();
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterActionStateBuilder<CT>.Goto(State targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterActionStateBuilder<CT>.Loop()
        {
            this.Loop();
            return this;
        }

        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterConditionedActionStateBuilder<CT>.Goto(State targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterConditionedActionStateBuilder<CT>.Loop()
        {
            this.Loop();
            return this;
        }

        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterPreconditionStateBuilder<CT>.Goto(Func<CT, State> targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterActionStateBuilder<CT>.Goto(Func<CT, State> targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterConditionedActionStateBuilder<CT>.Goto(Func<CT, State> targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterFinalPreconditionStateBuilder<CT>.Goto(Func<CT, State> targetState)
        {
            this.Goto(targetState);
            return this;
        }

        IAfterActionStateBuilderWithoutTransition<CT> IAfterActionStateBuilder<CT>.Finish()
        {
            this.Finish();
            return this;
        }

        IAfterConditionedStateBuilderWithoutTransition<CT> IAfterConditionedActionStateBuilder<CT>.Finish()
        {
            this.Finish();
            return this;
        }

        IAfterConditionedActionStateBuilder<CT> IAfterPreconditionStateBuilder<CT>.Wait(Func<CT, TimeSpan> waitingTime)
        {
            this.Wait(waitingTime);
            return this;
        }

        IAfterConditionedActionStateBuilder<CT> IAfterPreconditionStateBuilder<CT>.Wait(TimeSpan waitingTime)
        {
            this.Wait(waitingTime);
            return this;
        }

        IAfterConditionedActionStateBuilder<CT> IAfterPreconditionStateBuilder<CT>.WaitForAll(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout)
        {
            this.WaitForAll(waitHandles, timeout);
            return this;
        }

        IAfterConditionedActionStateBuilder<CT> IAfterPreconditionStateBuilder<CT>.WaitForAny(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout)
        {
            this.WaitForAny(waitHandles, timeout);
            return this;
        }

        IAfterConditionedActionStateBuilder<CT> IAfterPreconditionStateBuilder<CT>.WaitFor(Func<CT, IAsyncWaitHandle> waitHandle, Func<CT, TimeSpan> timeout)
        {
            this.WaitFor(waitHandle, timeout);
            return this;
        }
    }

    public interface IInitialStateBuilder<CT> where CT : class, IStateMachineContext
    {
        // Next Step
        IAfterStartStateBuilder<CT> Step(string description = "");
    }

    public interface IAfterStartStateBuilder<CT> where CT : class, IStateMachineContext
    {
        //Preconditions
        IAfterPreconditionStateBuilder<CT> If(Func<CT, Task<bool>> predicate);

        IAfterPreconditionStateBuilder<CT> If(Func<CT, bool> predicate);

        IAfterWhileStateBuilder<CT> While(Func<CT, Task<bool>> predicate);

        IAfterWhileStateBuilder<CT> While(Func<CT, bool> predicate);

        //Actions
        IAfterActionStateBuilder<CT> Do(Func<CT, Task> action);

        IAfterActionStateBuilder<CT> Do(Action<CT> action);

        IAfterActionStateBuilder<CT> Wait(Func<CT, TimeSpan> waitingTime);

        IAfterActionStateBuilder<CT> Wait(TimeSpan waitingTime);

        IAfterActionStateBuilder<CT> WaitForAll(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterActionStateBuilder<CT> WaitForAny(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterActionStateBuilder<CT> WaitFor(Func<CT, IAsyncWaitHandle> waitHandle, Func<CT, TimeSpan> timeout = default);

        //Transitions
        State<CT> Goto(State targetState);

        State<CT> Goto(Func<CT, State> targetState);

        State<CT> Loop();

        State<CT> Finish();

        IAfterStartStateBuilder<CT> Step(string description = "");
    }

    public interface IAfterPreconditionStateBuilder<CT> where CT : class, IStateMachineContext
    {
        //Actions
        IAfterConditionedActionStateBuilder<CT> Do(Func<CT, Task> action);

        IAfterConditionedActionStateBuilder<CT> Do(Action<CT> action);

        IAfterConditionedActionStateBuilder<CT> Wait(Func<CT, TimeSpan> waitingTime);

        IAfterConditionedActionStateBuilder<CT> Wait(TimeSpan waitingTime);

        IAfterConditionedActionStateBuilder<CT> WaitForAll(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterConditionedActionStateBuilder<CT> WaitForAny(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterConditionedActionStateBuilder<CT> WaitFor(Func<CT, IAsyncWaitHandle> waitHandle, Func<CT, TimeSpan> timeout = default);

        //Transitions
        IAfterConditionedStateBuilderWithoutTransition<CT> Goto(State targetState);
        IAfterConditionedStateBuilderWithoutTransition<CT> Goto(Func<CT, State> targetState);

        IAfterConditionedStateBuilderWithoutTransition<CT> Loop();

        IAfterConditionedStateBuilderWithoutTransition<CT> Finish();
    }

    public interface IAfterWhileStateBuilder<CT> where CT : class, IStateMachineContext
    {
        //Actions
        IAfterActionStateBuilder<CT> Do(Func<CT, Task> action);

        IAfterActionStateBuilder<CT> Do(Action<CT> action);

        IAfterActionStateBuilder<CT> Wait(Func<CT, TimeSpan> waitingTime);

        IAfterActionStateBuilder<CT> Wait(TimeSpan waitingTime);

        IAfterActionStateBuilder<CT> WaitForAll(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterActionStateBuilder<CT> WaitForAny(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterActionStateBuilder<CT> WaitFor(Func<CT, IAsyncWaitHandle> waitHandle, Func<CT, TimeSpan> timeout = default);

    }

    public interface IAfterFinalPreconditionStateBuilder<CT> where CT : class, IStateMachineContext
    {
        //Actions
        IAfterActionStateBuilder<CT> Do(Func<CT, Task> action);

        IAfterActionStateBuilder<CT> Do(Action<CT> action);

        IAfterActionStateBuilder<CT> Wait(Func<CT, TimeSpan> waitingTime);

        IAfterActionStateBuilder<CT> Wait(TimeSpan waitingTime);

        IAfterActionStateBuilder<CT> WaitForAll(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterActionStateBuilder<CT> WaitForAny(Func<CT, IAsyncWaitHandle[]> waitHandles, Func<CT, TimeSpan> timeout = default);

        IAfterActionStateBuilder<CT> WaitFor(Func<CT, IAsyncWaitHandle> waitHandle, Func<CT, TimeSpan> timeout = default);

        //Transitions
        IAfterActionStateBuilderWithoutTransition<CT> Goto(State targetState);
        IAfterActionStateBuilderWithoutTransition<CT> Goto(Func<CT, State> targetState);

        IAfterActionStateBuilderWithoutTransition<CT> Loop();

        IAfterActionStateBuilderWithoutTransition<CT> Finish();
    }

    public interface IAfterConditionedActionStateBuilder<CT> where CT : class, IStateMachineContext
    {
        IAfterFinalPreconditionStateBuilder<CT> Else();

        IAfterPreconditionStateBuilder<CT> ElseIf(Func<CT, Task<bool>> predicate);

        IAfterPreconditionStateBuilder<CT> ElseIf(Func<CT, bool> predicate);

        //Descriptions
        IAfterActionStateBuilder<CT> Using(Func<CT, object> resource);

        //Transitions
        IAfterConditionedStateBuilderWithoutTransition<CT> Goto(State targetState);
        IAfterConditionedStateBuilderWithoutTransition<CT> Goto(Func<CT, State> targetState);

        IAfterConditionedStateBuilderWithoutTransition<CT> Loop();
        IAfterConditionedStateBuilderWithoutTransition<CT> Finish();

        //ExceptionHandling
        INextStateBuilder<CT> Catch();

        INextStateBuilder<CT> CatchAndDo(Func<CT, Exception, Task> action);

        INextStateBuilder<CT> CatchAndDo(Action<CT, Exception> action);

        INextStateBuilder<CT> CatchAndGoto(State state);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, Task<bool>> predicate);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, bool> predicate);

        // Next Step
        IAfterStartStateBuilder<CT> Step(string description = "");
    }

    public interface IAfterConditionedStateBuilderWithoutTransition<CT> where CT : class, IStateMachineContext
    {
        IAfterFinalPreconditionStateBuilder<CT> Else();

        IAfterPreconditionStateBuilder<CT> ElseIf(Func<CT, Task<bool>> predicate);

        IAfterPreconditionStateBuilder<CT> ElseIf(Func<CT, bool> predicate);

        //Descriptions
        IAfterActionStateBuilder<CT> Using(Func<CT, object> resource);

        //ExceptionHandling
        INextStateBuilder<CT> Catch();

        INextStateBuilder<CT> CatchAndDo(Func<CT, Exception, Task> action);

        INextStateBuilder<CT> CatchAndDo(Action<CT, Exception> action);

        INextStateBuilder<CT> CatchAndGoto(State state);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, Task<bool>> predicate);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, bool> predicate);

        // Next Step
        IAfterStartStateBuilder<CT> Step(string description = "");
    }

    public interface IAfterActionStateBuilder<CT> where CT : class, IStateMachineContext
    {
        //Descriptions
        IAfterActionStateBuilder<CT> Using(Func<CT, object> resource);

        //Transitions
        IAfterActionStateBuilderWithoutTransition<CT> Goto(State targetState);
        IAfterActionStateBuilderWithoutTransition<CT> Goto(Func<CT, State> targetState);

        IAfterActionStateBuilderWithoutTransition<CT> Loop();

        IAfterActionStateBuilderWithoutTransition<CT> Finish();

        //ExceptionHandling
        INextStateBuilder<CT> Catch();

        INextStateBuilder<CT> CatchAndDo(Func<CT, Exception, Task> action);

        INextStateBuilder<CT> CatchAndDo(Action<CT, Exception> action);

        INextStateBuilder<CT> CatchAndGoto(State state);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, Task<bool>> predicate);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, bool> predicate);

        // Next Step
        IAfterStartStateBuilder<CT> Step(string description = "");
    }

    public interface IAfterActionStateBuilderWithoutTransition<CT> where CT : class, IStateMachineContext
    {
        //Descriptions
        IAfterActionStateBuilder<CT> Using(Func<CT, object> resource);

        //ExceptionHandling
        INextStateBuilder<CT> Catch();

        INextStateBuilder<CT> CatchAndDo(Func<CT, Exception, Task> action);

        INextStateBuilder<CT> CatchAndDo(Action<CT, Exception> action);

        INextStateBuilder<CT> CatchAndGoto(State state);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, Task<bool>> predicate);

        INextStateBuilder<CT> CatchAndRepeatIf(Func<CT, Exception, bool> predicate);

        // Next Step
        IAfterStartStateBuilder<CT> Step(string description = "");
    }

    public interface INextStateBuilder<CT> where CT : class, IStateMachineContext
    {
        // Next Step
        IAfterStartStateBuilder<CT> Step(string description = "");
    }
}