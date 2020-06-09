using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Services.MetaData;
using FeatureFlowFramework.Workflows;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Services.Logging
{
    public class WorkflowExecutionInfoLogger : IDataFlowSink
    {
        private readonly List<Predicate<Workflow.ExecutionInfo>> filters = new List<Predicate<Workflow.ExecutionInfo>>();

        public WorkflowExecutionInfoLogger(bool filterDefaultFileLogger = true)
        {
            filters.Add(msg => !(msg.workflow is DefaultFileLogger));
        }

        public WorkflowExecutionInfoLogger(bool filterDefaultFileLogger, params Predicate<Workflow.ExecutionInfo>[] filters)
        {
            this.filters.Add(msg => !(msg.workflow is DefaultFileLogger));
            this.filters.AddRange(filters);
        }

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
            foreach (var filter in filters) if (!filter(executionInfo)) return;

            var wf = executionInfo.workflow;
            var evnt = executionInfo.executionEvent;
            var phase = executionInfo.executionPhase;
            var stateIndex = executionInfo.executionState.stateIndex;
            var stepIndex = executionInfo.executionState.stepIndex;
            IWorkflowInfo wfInfo = wf;
            var state = wfInfo.StateMachineInfo.StateInfos[stateIndex];
            var stateName = state.Name;
            var stepName = state.StepInfos[stepIndex].Description;

            Log.TRACE(wf.GetHandle(), $"Workflow {wf.Name}, event: {evnt}, phase: {phase}, state: {stateIndex}({stateName}), step: {stepIndex}({stepName}).");
        }
    }
}