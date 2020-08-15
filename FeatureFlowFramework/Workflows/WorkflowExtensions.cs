using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Workflows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public static class WorkflowExtensions
    {
        public static WorkflowStopper RequestStopWhen(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, bool tryCancelWaitingState, bool deactivateWhenFired)
        {
            return new WorkflowStopper(workflow, condition, tryCancelWaitingState, deactivateWhenFired);
        }

        public static async Task<bool> WaitUntilAsync(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, TimeSpan timeout = default, bool ignoreCurrentState = false)
        {
            if (!ignoreCurrentState) if(condition(workflow.CreateExecutionInfo())) return true;

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> trigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(condition);
            workflow.ExecutionInfoSource.ConnectTo(trigger);
            if(!ignoreCurrentState) trigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if(timeout == default) await trigger.WaitAsync();
            else success = await trigger.WaitAsync(timeout);

            workflow.ExecutionInfoSource.DisconnectFrom(trigger);
            return success;
        }

        public static bool WaitUntil(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, TimeSpan timeout = default, bool ignoreCurrentState = false)
        {
            if(!ignoreCurrentState) if(condition(workflow.CreateExecutionInfo())) return true;

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> trigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(condition);
            workflow.ExecutionInfoSource.ConnectTo(trigger);
            if(!ignoreCurrentState) trigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if(timeout == default) trigger.Wait();
            else success = trigger.Wait(timeout);

            workflow.ExecutionInfoSource.DisconnectFrom(trigger);
            return success;
        }

        public static async Task<bool> WaitUntilAsync(this Workflow workflow, Predicate<Workflow.ExecutionInfo> startCondition, Predicate<Workflow.ExecutionInfo> endCondition, TimeSpan timeout = default)
        {
            TimeFrame timeFrame = new TimeFrame(timeout);

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> startTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(startCondition);
            workflow.ExecutionInfoSource.ConnectTo(startTrigger);
            startTrigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if(timeout == default) await startTrigger.WaitAsync();
            else success = await startTrigger.WaitAsync(timeFrame.Remaining);

            workflow.ExecutionInfoSource.DisconnectFrom(startTrigger);

            if(!success) return false;
            else
            {
                ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> endTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(endCondition);
                workflow.ExecutionInfoSource.ConnectTo(endTrigger);
                endTrigger.Post(workflow.CreateExecutionInfo());

                if(timeout == default) await endTrigger.WaitAsync();
                else success = await endTrigger.WaitAsync(timeFrame.Remaining);

                workflow.ExecutionInfoSource.DisconnectFrom(endTrigger);
            }
            return success;
        }

        public static bool WaitUntil(this Workflow workflow, Predicate<Workflow.ExecutionInfo> startCondition, Predicate<Workflow.ExecutionInfo> endCondition, TimeSpan timeout = default)
        {
            TimeFrame timeFrame = new TimeFrame(timeout);

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> startTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(startCondition);
            workflow.ExecutionInfoSource.ConnectTo(startTrigger);
            startTrigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if(timeout == default) startTrigger.Wait();
            else success = startTrigger.Wait(timeFrame.Remaining);

            workflow.ExecutionInfoSource.DisconnectFrom(startTrigger);

            if(!success) return false;
            else
            {
                ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> endTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(endCondition);
                workflow.ExecutionInfoSource.ConnectTo(endTrigger);
                endTrigger.Post(workflow.CreateExecutionInfo());

                if(timeout == default) endTrigger.Wait();
                else success = endTrigger.Wait(timeFrame.Remaining);

                workflow.ExecutionInfoSource.DisconnectFrom(endTrigger);
            }
            return success;
        }

        public static bool WaitUntilFinished(this Workflow workflow, TimeSpan timeout = default)
        {
            return workflow.WaitUntil(info => info.executionEvent == Workflow.ExecutionEventList.WorkflowFinished, timeout);
        }
    }
}
