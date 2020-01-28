using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public interface IAsyncManualResetEvent : IAsyncWaitHandle
    {
        IAsyncWaitHandle AsyncWaitHandle { get; }
        bool IsSet { get; }
        void Reset();
        void Set();
        void SetAndReset();
    }
}