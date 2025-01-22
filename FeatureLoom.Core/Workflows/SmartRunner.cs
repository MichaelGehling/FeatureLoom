using FeatureLoom.Time;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public class SmartRunner : AbstractRunner
    {
        ConcurrentDictionary<Type, bool[][]> stateMachineIsAsyncStepCache = new ConcurrentDictionary<Type, bool[][]>();
        
        public Action<Workflow> OnWorkflowStart { get; set; }
        public Action<Workflow> OnWorkflowEnd { get; set; }

        public override async Task RunAsync(Workflow workflow)
        {
            OnWorkflowStart?.Invoke(workflow);
            AddToRunningWorkflows(workflow);
            try
            {
                if (!stateMachineIsAsyncStepCache.TryGetValue(workflow.GetType(), out bool[][] isAsyncMap))
                {
                    isAsyncMap = PrepareIsAsyncMap(workflow);
                    stateMachineIsAsyncStepCache[workflow.GetType()] = isAsyncMap;
                }

                bool running;
                do
                {
                    var executionState = workflow.CurrentExecutionState;
                    if (isAsyncMap[executionState.stateIndex][executionState.stepIndex]) running = await workflow.ExecuteNextStepAsync(executionController).ConfigureAwait(false);
                    else running = workflow.ExecuteNextStep(executionController);
                }
                while (running);
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
                OnWorkflowEnd?.Invoke(workflow);
            }
        }        
        

        private static bool[][] PrepareIsAsyncMap(Workflow workflow)
        {
            var stateInfos = workflow.StateMachineInfo.StateInfos;
            bool[][] isAsyncMap = new bool[stateInfos.Length][];
            for (int i = 0; i < stateInfos.Length; i++)
            {
                var stepInfos = stateInfos[i].StepInfos;
                isAsyncMap[i] = new bool[stepInfos.Length];
                for (int j = 0; j < stepInfos.Length; j++)
                {
                    isAsyncMap[i][j] = stepInfos[j].IsAsync;
                }
            }

            return isAsyncMap;
        }
    }
}