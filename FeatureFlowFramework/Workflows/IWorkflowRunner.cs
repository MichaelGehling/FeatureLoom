using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public interface IWorkflowRunner
    {
        void Run(IWorkflowControls workflow);
        IEnumerable<IWorkflowControls> RunningWorkflows { get; }
        Task PauseAllWorkflows();
    }
}
