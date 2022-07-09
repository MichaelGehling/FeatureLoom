using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public class Step<CT> : PartialStep<CT>, IStepInfo where CT : class, IStateMachineContext
    {
        public StateMachine<CT> parentStateMachine;
        public State<CT> parentState;
        public string description = "";
        public int stepIndex;

        public bool hasCatchException = false;
        public State onExceptionTargetState;
        public Func<CT, Exception, Task> onExceptionAsync;
        public Action<CT, Exception> onException;
        public Func<CT, Exception, bool> onExceptionRepeatCondition;
        public Func<CT, Exception, Task<bool>> onExceptionRepeatConditionAsync;

        public List<Func<CT, object>> usingResourcesDelegates;
        private bool? isAsync = null;

        internal Step(StateMachine<CT> parentStateMachine, State<CT> parentState, int stepIndex)
        {
            this.parentStateMachine = parentStateMachine;
            this.parentState = parentState;
            this.stepIndex = stepIndex;
        }

        public new bool IsAsync
        {
            get
            {
                if (!isAsync.HasValue)
                {
                    isAsync = onExceptionAsync != null ||
                              actionAsync != null ||
                              conditionAsync != null ||
                              onExceptionRepeatConditionAsync != null ||
                              waitingAsyncDelegate != null ||
                              (doElse?.IsAsync ?? false);

                }
                return isAsync.Value;
            }
        }


        public string Description => description;

        public IStateInfo[] TargetStates
        {
            get
            {
                LazyValue<List<IStateInfo>> result = new LazyValue<List<IStateInfo>>();
                var nextElse = doElse;
                while (nextElse != null)
                {
                    if (nextElse.targetStates != null)
                    {
                        result.Obj.AddRange(nextElse.targetStates);
                    }
                    nextElse = nextElse.doElse;
                }

                if (!result.Exists)
                {
                    if (targetStates == null) return Array.Empty<IStateInfo>();
                    else return targetStates.ToArray();
                }
                else
                {
                    if (targetStates != null) result.Obj.AddRange(targetStates);
                    return result.Obj.ToArray();
                }
            }
        }

        public bool MayTerminate
        {
            get
            {
                if (finishStateMachine) return true;
                var nextElse = doElse;
                while (nextElse != null)
                {
                    if (nextElse.finishStateMachine) return true;
                    nextElse = nextElse.doElse;
                }
                return false;
            }
        }

        public int StepIndex => stepIndex;

        public void AddUsingResource(Func<CT, object> resourceDelegate)
        {
            if (usingResourcesDelegates == null) usingResourcesDelegates = new List<Func<CT, object>>();
            usingResourcesDelegates.Add(resourceDelegate);
        }
    }

    public class PartialStep<CT> where CT : IStateMachineContext
    {
        public bool hasCondition = false;
        public Func<CT, Task<bool>> conditionAsync;
        public Func<CT, bool> condition;
        public bool repeatWhileCondition = false;
        public PartialStep<CT> doElse = null;

        public bool hasAction = false;
        public Func<CT, Task> actionAsync;
        public Action<CT> action;

        public bool hasWaiting = false;

        public Action<CT> waitingDelegate;

        public Func<CT, Task> waitingAsyncDelegate;

        public bool finishStateMachine = false;
        public Func<CT, int> targetStateIndex;
        public State[] targetStates;

        public bool IsAsync => actionAsync != null || conditionAsync != null || waitingAsyncDelegate != null;
        
    }
}