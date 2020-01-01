namespace FeatureFlowFramework.Workflows
{
    public readonly struct WorkflowExecutionState
    {
        readonly public int stateIndex;
        readonly public int stepIndex;

        public WorkflowExecutionState(int stateIndex, int stepIndex)
        {
            this.stateIndex = stateIndex;
            this.stepIndex = stepIndex;
        }
    }
}