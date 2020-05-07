using FeatureFlowFramework.Aspects;
using FeatureFlowFramework.Aspects.AppStructure;
using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public abstract partial class Workflow : IWorkflowInfo, IStateMachineContext, IUpdateAppStructureAspect
    {                
        protected long id;
        protected ExecutionState executionState;
        protected ExecutionPhase executionPhase = ExecutionPhase.Prepared;
        protected IWorkflowRunner currentRunner;        

        public IWorkflowRunner Runner
        {
            get => currentRunner;
            set => currentRunner = value;
        }

        [JsonIgnore]
        protected ControlData controlData = ControlData.Init();

        protected LazySlim<Sender> executionInfoSender;        

        public virtual bool TryUpdateAppStructureAspects(TimeSpan timeout)
        {
            if (executionInfoSender.IsInstantiated)
            {
                var childrenInterface = this.GetAspectInterface<IAcceptsChildren, AppStructureAddOn>();
                childrenInterface.AddChild(executionInfoSender.Obj);
            }
            return true;
        }

        [JsonIgnore]
        public IDataFlowSource ExecutionInfoSource => executionInfoSender.Obj;

        [JsonIgnore]
        protected virtual IWorkflowRunner DefaultRunner => WorkflowRunnerService.DefaultRunner;

        public virtual bool AddToAspectRegistry => true;

        [JsonIgnore]
        public ExecutionState CurrentExecutionState { get => executionState; set => executionState = value; }

        [JsonIgnore]
        public ExecutionPhase CurrentExecutionPhase => executionPhase;

        [JsonIgnore]
        private readonly ExecutionEventList executionEvents = new ExecutionEventList();

        [JsonIgnore]
        public virtual ExecutionEventList ExecutionEvents => executionEvents;

        [JsonIgnore]
        public string Name => $"{this.GetType().ToString()}_{this.id}";

        [JsonIgnore]
        bool IStateMachineContext.PauseRequested
        {
            get { return controlData.pauseRequested; }
            set { controlData.pauseRequested = value; }
        }

        [JsonIgnore]
        string IStateMachineContext.ContextName => Name;

        [JsonIgnore]
        long IStateMachineContext.ContextId => id;

        [JsonIgnore]
        ExecutionPhase IStateMachineContext.ExecutionPhase
        {
            get => executionPhase;
            set
            {
                executionPhase = value;
            }
        }

        void IStateMachineContext.SendExecutionInfoEvent(string executionEvent, object additionalInfo)
        {
            executionInfoSender.ObjIfExists?.Send(new ExecutionInfo(this, executionEvent, additionalInfo));
        }

        void IStateMachineContext.SendExecutionInfoEvent(string executionEvent, ExecutionState state, ExecutionPhase phase, object additionalInfo)
        {
            executionInfoSender.ObjIfExists?.Send(new ExecutionInfo(this, executionEvent, state, phase, additionalInfo));
        }

        public ExecutionInfo CreateExecutionInfo()
        {
            return new ExecutionInfo(this, ExecutionEventList.InfoRequested);
        }

        [JsonIgnore]
        protected abstract StateMachine WorkflowStateMachine { get; }

        public async Task<bool> ExecuteNextStepAsync(IStepExecutionController controller)
        {
            bool result = true;
            result = await WorkflowStateMachine.ExecuteNextStepAsync(this, controller);
            return result;
        }

        public bool ExecuteNextStep(IStepExecutionController controller)
        {
            bool result = true;
            result = WorkflowStateMachine.ExecuteNextStep(this, controller);
            return result;
        }

        public void Run(IWorkflowRunner runner = null)
        {
            runner = runner ?? this.currentRunner ?? DefaultRunner;
            runner.Run(this);
        }

        public void RequestPause(bool tryCancelWaitingStep)
        {
            controlData.pauseRequested = true;
            if(tryCancelWaitingStep) this.TryCancelWaiting();
        }

        [JsonIgnore]
        public CancellationToken CancellationToken
        {
            get
            {
                if (this.controlData.cancellationTokenSource == null) this.controlData.cancellationTokenSource = new CancellationTokenSource();
                return this.controlData.cancellationTokenSource.Token;
            }
        }

        [JsonIgnore]
        public bool IsRunning => this.CurrentExecutionPhase == ExecutionPhase.Running || this.CurrentExecutionPhase == ExecutionPhase.Waiting;

        protected void TryCancelWaiting()
        {
            this.controlData.cancellationTokenSource?.Cancel();
            this.controlData.cancellationTokenSource?.Dispose();
            this.controlData.cancellationTokenSource = null;
        }

        public IStateMachineInfo StateMachineInfo => WorkflowStateMachine;

    }

    public abstract class Workflow<SM> : Workflow where SM : StateMachine, new()
    {
        protected Workflow(string name = null)
        {
            executionState = WorkflowStateMachine.InitialExecutionState;
            if (AddToAspectRegistry) id = this.GetAspectHandle();
            else id = RandomGenerator.Int64();
        }

        protected static SM stateMachineInstance = new SM();

        [JsonIgnore]
        protected override StateMachine WorkflowStateMachine => stateMachineInstance;
    }
}