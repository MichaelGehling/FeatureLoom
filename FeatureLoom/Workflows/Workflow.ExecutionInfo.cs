namespace FeatureLoom.Workflows
{
    public abstract partial class Workflow
    {
        public struct ExecutionInfo
        {
            public readonly Workflow workflow;
            public readonly string executionEvent;
            public readonly ExecutionState executionState;
            public readonly ExecutionPhase executionPhase;
            public readonly object additionalInfo;

            public ExecutionInfo(Workflow workflow, string executionEvent, ExecutionState executionState, ExecutionPhase executionPhase, object additionalInfo = null)
            {
                this.workflow = workflow;
                this.executionEvent = executionEvent;
                this.executionState = executionState;
                this.executionPhase = executionPhase;
                this.additionalInfo = additionalInfo;
            }

            public ExecutionInfo(IStateMachineContext context, string executionEvent, object additionalInfo = null)
            {
                this.workflow = context as Workflow;
                this.executionEvent = executionEvent;
                this.executionState = context.CurrentExecutionState;
                this.executionPhase = context.ExecutionPhase;
                this.additionalInfo = additionalInfo;
            }
        }
    }
}