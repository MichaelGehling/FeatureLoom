using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;

using FeatureLoom.MetaDatas;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.DependencyInversion;

namespace FeatureLoom.Workflows
{
    public abstract partial class Workflow : IWorkflowInfo, IStateMachineContext
    {
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

        protected LazyValue<Sender> executionInfoSender;

        [JsonIgnore]
        public IMessageSource ExecutionInfoSource => executionInfoSender.Obj;

        [JsonIgnore]
        protected virtual IWorkflowRunner DefaultRunner => Service<WorkflowRunnerService>.Instance.DefaultRunner;

        [JsonIgnore]
        public ExecutionState CurrentExecutionState { get => executionState; set => executionState = value; }

        [JsonIgnore]
        public ExecutionPhase CurrentExecutionPhase => executionPhase;

        [JsonIgnore]
        private readonly ExecutionEventList executionEvents = new ExecutionEventList();

        [JsonIgnore]
        public virtual ExecutionEventList ExecutionEvents => executionEvents;

        [JsonIgnore]
        public string Name => $"{this.GetType()}_{this.GetHandle().ToString()}";

        [JsonIgnore]
        bool IStateMachineContext.PauseRequested
        {
            get { return controlData.pauseRequested; }
            set { controlData.pauseRequested = value; }
        }

        [JsonIgnore]
        string IStateMachineContext.ContextName => Name;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> ExecuteNextStepAsync(IStepExecutionController controller)
        {
            return WorkflowStateMachine.ExecuteNextStepAsync(this, controller);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ExecuteNextStep(IStepExecutionController controller)
        {
            return WorkflowStateMachine.ExecuteNextStep(this, controller);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNextStepAsync()
        {            
            return WorkflowStateMachine.IsStepAsync(CurrentExecutionState);
        }

        public void Run(IWorkflowRunner runner = null)
        {
            runner = runner ?? this.currentRunner ?? DefaultRunner;
            _ = runner.RunAsync(this);
        }

        public void RequestPause(bool tryCancelWaitingStep)
        {
            controlData.pauseRequested = true;
            if (tryCancelWaitingStep) this.TryCancelWaiting();
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
        }

        protected static SM stateMachineInstance = new SM();

        [JsonIgnore]
        protected override StateMachine WorkflowStateMachine => stateMachineInstance;
    }
}