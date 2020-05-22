using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Workflows;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Services.Logging
{
    public class WorkflowExecutionInfoLogger : IDataFlowSink
    {
        //TODO use Event Filter
        private readonly List<string> eventFilter;

        public void Post<M>(in M message)
        {
            if(message is Workflow.ExecutionInfo executionInfo) LogInfo(executionInfo);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is Workflow.ExecutionInfo executionInfo) LogInfo(executionInfo);
            return Task.CompletedTask;
        }

        private void LogInfo(Workflow.ExecutionInfo executionInfo)
        {
            var wf = executionInfo.workflow;
            if(wf is DefaultFileLogger) return;

            var evnt = executionInfo.executionEvent;
            var phase = executionInfo.executionPhase;
            var stateIndex = executionInfo.executionState.stateIndex;
            var stepIndex = executionInfo.executionState.stepIndex;
            IWorkflowInfo wfInfo = wf;
            var state = wfInfo.StateMachineInfo.StateInfos[stateIndex];
            var stateName = state.Name;
            var stepName = state.StepInfos[stepIndex].Description;

            Log.TRACE(wf, $"Workflow {wf.Name}, event: {evnt}, phase: {phase}, state: {stateIndex}({stateName}), step: {stepIndex}({stepName}).");
        }
    }
}