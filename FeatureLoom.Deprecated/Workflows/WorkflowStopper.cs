﻿using FeatureLoom.MessageFlow;
using System;

namespace FeatureLoom.Workflows
{
    public class WorkflowStopper
    {
        private Workflow workflow;
        private readonly Predicate<Workflow.ExecutionInfo> predicate;
        private readonly ProcessingEndpoint<Workflow.ExecutionInfo> processor;

        public WorkflowStopper(Workflow workflow, Predicate<Workflow.ExecutionInfo> predicate, bool tryCancelWaitingState, bool deactivateWhenFired)
        {
            this.workflow = workflow;
            this.predicate = predicate;
            this.processor = new ProcessingEndpoint<Workflow.ExecutionInfo>(info =>
            {
                if (predicate(info))
                {
                    workflow.RequestPause(tryCancelWaitingState);
                    if (deactivateWhenFired) Deactivate();
                }
            });
            Activate();
        }

        public void Activate()
        {
            workflow.ExecutionInfoSource.ConnectTo(processor);
        }

        public void Deactivate()
        {
            workflow.ExecutionInfoSource.DisconnectFrom(processor);
        }
    }
}