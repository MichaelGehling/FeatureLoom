using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;

namespace FeatureFlowFramework.Workflows
{
    public class State<CT> : State where CT : IStateMachineContext
    {
        public readonly List<Step<CT>> steps = new List<Step<CT>>();

        public readonly StateMachine<CT> parentStateMachine;

        internal State(StateMachine<CT> parentStateMachine, int stateIndex, string name, string description)
        {
            this.parentStateMachine = parentStateMachine;
            this.stateIndex = stateIndex;
            this.name = name;
            this.description = description;
        }

        public override IStepInfo[] StepInfos => steps.ToArray();

        public IInitialStateBuilder<CT> Build(string description = "")
        {
            var builder = parentStateMachine.BuildState(this.name, description) as StateBuilder<CT>;
            if(builder.state != this)
            {
                Log.ERROR(parentStateMachine, $"Tried to build state object {this.name}, not part of this statemachine {parentStateMachine.GetType().FullName}!");
                throw new Exception($"Tried to build state object {this.name}, not part of this statemachine {parentStateMachine.GetType().FullName}!");
            }
            return builder;
        }

        public static implicit operator WorkflowExecutionState(State<CT> state) => new WorkflowExecutionState(state.stateIndex, 0);
    }

    public abstract class State : IStateInfo
    {
        public int stateIndex;
        public string name;
        public string description;

        public abstract IStepInfo[] StepInfos { get; }
        public string Name => name;
        public string Description => description;
        public int StateIndex => stateIndex;
    }

    public interface IStateInfo
    {
        IStepInfo[] StepInfos { get; }
        string Name { get; }
        string Description { get; }
        int StateIndex { get; }
    }
}
