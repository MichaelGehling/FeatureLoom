using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System;
using System.Windows.Forms;

namespace FeatureLoom.Forms
{
    public static partial class FormsExtensions
    {
        public class WorkflowApplicationContext : ApplicationContext
        {
            private Workflow workflow;
            private IWorkflowRunner runner;

            public WorkflowApplicationContext(Workflow workflow, IWorkflowRunner runner = null)
            {
                this.workflow = workflow;
                if (runner != null) this.runner = runner;
                else this.runner = new SmartRunner();
                Application.Idle += StartWorkflow;
            }

            public void StartWorkflow(object sender, EventArgs e)
            {
                Application.Idle -= StartWorkflow;

                workflow.Run(runner);
                workflow.ExecutionInfoSource.ConnectTo(new ProcessingEndpoint<Workflow.ExecutionInfo>(async msg =>
                {
                    if (msg.executionPhase == Workflow.ExecutionPhase.Finished ||
                       msg.executionPhase == Workflow.ExecutionPhase.Invalid)
                    {
                        if (!await runner.PauseAllWorkflows(true).WaitAsync(5.Seconds()))
                        {
                            throw new Exception($"Failed to stop all workflows!\n{runner.RunningWorkflows.AllItemsToString()}");
                        }
                        Application.ExitThread();
                    }
                }));
            }
        }
    }
}