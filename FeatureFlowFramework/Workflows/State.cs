using FeatureLoom.Services.Logging;
using FeatureLoom.Services.MetaData;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Workflows
{
    public class State<CT> : State where CT : class, IStateMachineContext
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
            var builder = parentStateMachine.BuildState(this, description) as StateBuilder<CT>;
            if(builder.state != this)
            {
                Log.ERROR(parentStateMachine.GetHandle(), $"Tried to build state object {this.name}, not part of this statemachine {parentStateMachine.GetType().FullName}!");
                throw new Exception($"Tried to build state object {this.name}, not part of this statemachine {parentStateMachine.GetType().FullName}!");
            }
            return builder;
        }

        public static implicit operator Workflow.ExecutionState(State<CT> state) => (state.stateIndex, 0);
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
}