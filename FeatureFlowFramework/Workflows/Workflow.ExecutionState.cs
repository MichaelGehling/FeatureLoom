namespace FeatureFlowFramework.Workflows
{
    public partial class Workflow
    {
        public readonly struct ExecutionState
        {
            readonly public int stateIndex;
            readonly public int stepIndex;

            public ExecutionState(int stateIndex, int stepIndex)
            {
                this.stateIndex = stateIndex;
                this.stepIndex = stepIndex;
            }

            public static implicit operator ExecutionState((int stateIndex, int stepIndex) tupel) => new ExecutionState(tupel.stateIndex, tupel.stepIndex);
            public override bool Equals(object obj)
            {
                return obj is ExecutionState state && 
                       state.stateIndex == this.stateIndex && 
                       state.stepIndex == this.stepIndex;
            }

            public bool Equals(int stateIndex, int stepIndex)
            {
                return stateIndex == this.stateIndex &&
                       stepIndex == this.stepIndex;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }        
    }
}