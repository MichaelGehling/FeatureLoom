namespace FeatureFlowFramework.Workflows
{
    public enum WorkflowExecutionPhase
    {
        Prepared,   // Initial phase after creation of the workflow
        Running,    // The workflow is currently executed by a WorkflowRunner
        Paused,     // The workflow was executed by a WorkflowRunner, but was stopped before being finished.
        Finished,   // The workflow reached one of its finish steps
        Invalid     // The workflow is in an invalid state (e.g. the state and step index do not match the statemachine's states or steps)
    }
}