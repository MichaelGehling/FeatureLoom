using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System;
using System.Windows.Forms;
using System.Threading;

namespace FeatureLoom.Forms
{

    public static partial class FormsExtensions
    {
        public class WorkflowApplicationContext : ApplicationContext
        {
            private Workflow workflow;
            private IWorkflowRunner runner;
            SynchronizationContext uiContext = null;

            public WorkflowApplicationContext(Workflow workflow, IWorkflowRunner runner = null)
            {
                uiContext = SynchronizationContext.Current;

                this.workflow = workflow;
                this.workflow.PrioritizeUiOverWorkflow();

                if (runner != null) this.runner = runner;
                else
                {
                    var smartRunner = new SmartRunner();
                    smartRunner.OnWorkflowStart = wf => wf.PrioritizeUiOverWorkflow();
                    smartRunner.OnWorkflowStart = wf => wf.StopWorkflowOnClosedUi();
                    this.runner = smartRunner;
                }

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
                        if (!await runner.PauseAllWorkflows(true).TryWaitAsync(5.Seconds()))
                        {
                            throw new Exception($"Failed to stop all workflows!\n{runner.RunningWorkflows.AllItemsToString()}");
                        }
                        uiContext.Post(_ => this.ExitThread(), null);
                    }
                }));
            }
        }
    }
}