using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public interface IAsyncWaitHandleSource
    {
        IAsyncWaitHandle AsyncWaitHandle { get; }
        Task WaitingTask { get; }
    }
}