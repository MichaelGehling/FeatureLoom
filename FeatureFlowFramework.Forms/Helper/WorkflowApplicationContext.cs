using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Workflows;
using System;
using System.Windows.Forms;

namespace FeatureFlowFramework.Helper
{
    public static partial class FormsExtensions
    {
        public class WorkflowApplicationContext : ApplicationContext
        {
            private Workflow workflow;
            private SuspendingAsyncRunner runner = new SuspendingAsyncRunner();

            public WorkflowApplicationContext(Workflow workflow)
            {
                this.workflow = workflow;
                Application.Idle += StartWorkflow;
            }

            public void StartWorkflow(object sender, EventArgs e)
            {
                Application.Idle -= StartWorkflow;

                workflow.Run(runner);
                workflow.ExecutionInfoSource.ConnectTo(new ProcessingEndpoint<Workflow.ExecutionInfo>(async msg =>
                {
                    if(msg.executionPhase == Workflow.ExecutionPhase.Finished ||
                       msg.executionPhase == Workflow.ExecutionPhase.Invalid)
                    {
                        if (!await runner.PauseAllWorkflows(true).WaitAsync(5.Seconds()))
                        {
                            throw new Exception("Failed to stop workflows!");
                        }
                        Application.ExitThread();
                    }
                }));
            }
        }
    }
}