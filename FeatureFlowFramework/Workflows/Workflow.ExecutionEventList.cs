namespace FeatureLoom.Workflows
{
    public abstract partial class Workflow
    {
        public class ExecutionEventList
        {
            public const string InfoRequested = "InfoRequested";

            public const string WorkflowStarted = "WorkflowStarted";
            public const string WorkflowFinished = "WorkflowFinished";
            public const string WorkflowPaused = "WorkflowPaused";
            public const string WorkflowInvalid = "WorkflowInvalid";
            public const string StepStarted = "StepStarted";
            public const string StepFinished = "StepFinished";
            public const string StepFailed = "StepFailed";
            public const string BeginWaiting = "BeginWaiting";
            public const string EndWaiting = "EndWaiting";
            public const string StateTransition = "StateTransition";

            public const string ExceptionCatched = "ExceptionCatched";
            public const string ExceptionNotCatched = "ExceptionNotCatched";
            public const string ExceptionWithRetry = "ExceptionWithRetry";
            public const string ExceptionWithoutRetry = "ExceptionWithoutRetry";
            public const string ExceptionWithAction = "ExceptionWithRetry";
            public const string ExceptionWithTransition = "ExceptionWithTransition";
        }
    }
}