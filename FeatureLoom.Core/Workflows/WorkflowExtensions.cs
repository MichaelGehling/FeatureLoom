using FeatureLoom.MessageFlow;
using FeatureLoom.Time;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public static class WorkflowExtensions
    {
        public static WorkflowStopper RequestStopWhen(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, bool tryCancelWaitingState, bool deactivateWhenFired)
        {
            return new WorkflowStopper(workflow, condition, tryCancelWaitingState, deactivateWhenFired);
        }

        public static async Task<bool> WaitUntilAsync(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, TimeSpan timeout = default, bool ignoreCurrentState = false)
        {
            if (!ignoreCurrentState) if (condition(workflow.CreateExecutionInfo())) return true;

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> trigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(condition);
            workflow.ExecutionInfoSource.ConnectTo(trigger);
            if (!ignoreCurrentState) trigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if (timeout == default) await trigger.WaitAsync().ConfigureAwait(false);
            else success = await trigger.WaitAsync(timeout).ConfigureAwait(false);

            workflow.ExecutionInfoSource.DisconnectFrom(trigger);
            return success;
        }

        public static bool WaitUntil(this Workflow workflow, Predicate<Workflow.ExecutionInfo> condition, TimeSpan timeout = default, bool ignoreCurrentState = false)
        {
            if (!ignoreCurrentState) if (condition(workflow.CreateExecutionInfo())) return true;

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> trigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(condition);
            workflow.ExecutionInfoSource.ConnectTo(trigger);
            if (!ignoreCurrentState) trigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if (timeout == default) trigger.Wait();
            else success = trigger.Wait(timeout);

            workflow.ExecutionInfoSource.DisconnectFrom(trigger);
            return success;
        }

        public static async Task<bool> WaitUntilAsync(this Workflow workflow, Predicate<Workflow.ExecutionInfo> startCondition, Predicate<Workflow.ExecutionInfo> endCondition, TimeSpan timeout = default)
        {
            TimeFrame timeFrame = new TimeFrame();

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> startTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(startCondition);
            workflow.ExecutionInfoSource.ConnectTo(startTrigger);
            startTrigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if (timeout == default) await startTrigger.WaitAsync().ConfigureAwait(false);
            else
            {
                DateTime now = AppTime.Now;
                timeFrame = new TimeFrame(now, timeout);
                success = await startTrigger.WaitAsync(timeFrame.Remaining(now)).ConfigureAwait(false);
            }

            workflow.ExecutionInfoSource.DisconnectFrom(startTrigger);

            if (!success) return false;
            else
            {
                ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> endTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(endCondition);
                workflow.ExecutionInfoSource.ConnectTo(endTrigger);
                endTrigger.Post(workflow.CreateExecutionInfo());

                if (timeout == default) await endTrigger.WaitAsync().ConfigureAwait(false);
                else success = await endTrigger.WaitAsync(timeFrame.Remaining()).ConfigureAwait(false);

                workflow.ExecutionInfoSource.DisconnectFrom(endTrigger);
            }
            return success;
        }

        public static bool WaitUntil(this Workflow workflow, Predicate<Workflow.ExecutionInfo> startCondition, Predicate<Workflow.ExecutionInfo> endCondition, TimeSpan timeout = default)
        {
            TimeFrame timeFrame = new TimeFrame();

            ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> startTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(startCondition);
            workflow.ExecutionInfoSource.ConnectTo(startTrigger);
            startTrigger.Post(workflow.CreateExecutionInfo());

            bool success = true;
            if (timeout == default) startTrigger.Wait();
            else
            {
                DateTime now = AppTime.Now;
                timeFrame = new TimeFrame(now, timeout);
                success = startTrigger.Wait(timeFrame.Remaining(now));
            }

            workflow.ExecutionInfoSource.DisconnectFrom(startTrigger);

            if (!success) return false;
            else
            {
                ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo> endTrigger = new ConditionalTrigger<Workflow.ExecutionInfo, Workflow.ExecutionInfo>(endCondition);
                workflow.ExecutionInfoSource.ConnectTo(endTrigger);
                endTrigger.Post(workflow.CreateExecutionInfo());

                if (timeout == default) endTrigger.Wait();
                else success = endTrigger.Wait(timeFrame.Remaining());

                workflow.ExecutionInfoSource.DisconnectFrom(endTrigger);
            }
            return success;
        }

        public static bool WaitUntilFinished(this Workflow workflow, TimeSpan timeout = default)
        {
            return workflow.WaitUntil(info => info.executionEvent == Workflow.ExecutionEventList.WorkflowFinished, timeout);
        }

        public static bool WaitUntilStopped(this Workflow workflow, TimeSpan timeout = default)
        {
            return workflow.WaitUntil(info => info.executionPhase != Workflow.ExecutionPhase.Running && info.executionPhase != Workflow.ExecutionPhase.Waiting, timeout);
        }
    }
}