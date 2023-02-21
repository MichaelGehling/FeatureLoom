using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Statemachines
{
    public sealed partial class Statemachine<T>
    {
        public sealed class Job : IStatemachineJob
        {       
            LazyValue<Sender<IStatemachineJob>> sender = new LazyValue<Sender<IStatemachineJob>>();            

            public void SendUpdate()
            {
                sender.ObjIfExists?.Send(this);
            }

            public bool PauseRequested { get; set; }
            public IMessageSource<IStatemachineJob> UpdateSource => sender.Obj;
            public CancellationToken CancellationToken { get; set; }
            public ExecutionState ExecutionState { get; set; }
            public string CurrentStateName { get; set; }
            public Exception Exception { get; set; }
            public Task ExecutionTask { get; set; }

            public T Context { get; set; }

            C IStatemachineJob.GetContext<C>() where C : class
            {
                return Context as C;
            }

            Type IStatemachineJob.GetContextType()
            {
                return Context.GetType();
            }

            void IStatemachineJob.SetContext<C>(C context) where C : class
            {
                if (context == null) throw new ArgumentNullException(nameof(context));
                if (!(context is T typedContext)) throw new ArgumentException($"Passed Context has wrong type: {typeof(C).ToString()} instead of {typeof(T).ToString()}!");
                Context = typedContext;
            }
        }

    }


        
}
