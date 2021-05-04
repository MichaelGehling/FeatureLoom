using FeatureLoom.DataFlows;
using FeatureLoom.Workflows;
using System;
using System.Windows.Forms;
using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Helpers.Time;
using FeatureLoom.Helpers.Extensions;

namespace FeatureLoom.Helpers.Forms
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
                            throw new Exception($"Failed to stop all workflows!\n{runner.RunningWorkflows.AllItemsToString()}");
                        }
                        Application.ExitThread();
                    }
                }));
            }
        }
    }
}