using FeatureLoom.MessageFlow;
using FeatureLoom.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public static class WorkflowExtensions
    {
        private static ProcessingEndpoint<Workflow.ExecutionInfo> processUiEventsOnWorkflowStep = new ProcessingEndpoint<Workflow.ExecutionInfo>(info =>
        {
            if (info.executionEvent == Workflow.ExecutionEventList.StepStarted) Application.DoEvents();
        });

        private static ProcessingEndpoint<Workflow.ExecutionInfo> stopWorkflowOnCloseUi = new ProcessingEndpoint<Workflow.ExecutionInfo>(info =>
        {
            if (info.executionEvent == Workflow.ExecutionEventList.StepFinished && Application.OpenForms.Count == 0)
            {
                info.workflow.RequestPause(true);
            }
        });

        public static void PrioritizeUiOverWorkflow(this Workflow workflow)
        {
            if (workflow.ExecutionInfoSource.CountConnectedSinks == 0 || !workflow.ExecutionInfoSource.GetConnectedSinks().Contains(processUiEventsOnWorkflowStep))
            {
                workflow.ExecutionInfoSource.ConnectTo(processUiEventsOnWorkflowStep);
            }
        }

        public static void StopWorkflowOnClosedUi(this Workflow workflow)
        {
            if (workflow.ExecutionInfoSource.CountConnectedSinks == 0 || !workflow.ExecutionInfoSource.GetConnectedSinks().Contains(stopWorkflowOnCloseUi))
            {
                workflow.ExecutionInfoSource.ConnectTo(stopWorkflowOnCloseUi);
            }
        }
    }
}
