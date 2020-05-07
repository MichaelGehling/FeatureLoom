using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Workflows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public static class WorkflowExtensions
    {
        public static WorkflowStopper StopWorkflowWhen(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, bool tryCancelWaitingState, bool deactivateWhenFired)
        {
            return new WorkflowStopper(workflow, condition, tryCancelWaitingState, deactivateWhenFired);
        }

        public static async Task<bool> WaitUntilAsync(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, TimeSpan timeout = default)
        {
            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> trigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(condition);
            workflow.ExecutionInfoSource.ConnectTo(trigger);
            trigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if(timeout == default) await trigger.WaitAsync();
            else success = await trigger.WaitAsync(timeout);

            workflow.ExecutionInfoSource.DisconnectFrom(trigger);
            return success;
        }

        public static bool WaitUntil(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, TimeSpan timeout = default)
        {
            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> trigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(condition);
            workflow.ExecutionInfoSource.ConnectTo(trigger);
            trigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if(timeout == default) trigger.Wait();
            else success = trigger.Wait(timeout);

            workflow.ExecutionInfoSource.DisconnectFrom(trigger);
            return success;
        }
    }
}
