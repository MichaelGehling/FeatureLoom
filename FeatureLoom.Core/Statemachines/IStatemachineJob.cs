using FeatureLoom.MessageFlow;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.Statemachines
{
    public interface IStatemachineJob
    {
        ExecutionState ExecutionState { get; set; }
        string CurrentStateName { get; set; }
        Type GetContextType();
        T GetContext<T>() where T : class;
        void SetContext<T>(T context) where T : class;
        Task ExecutionTask { get; }
        Exception Exception { get; }
        IMessageSource<IStatemachineJob> UpdateSource { get; }
        bool PauseRequested { get; set; }
    }


        
}
