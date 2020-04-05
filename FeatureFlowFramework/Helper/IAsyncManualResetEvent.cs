namespace FeatureFlowFramework.Helper
{
    public interface IAsyncManualResetEvent : IAsyncWaitHandle
    {
        IAsyncWaitHandle AsyncWaitHandle { get; }
        bool IsSet { get; }

        bool Reset();

        bool Set();
    }
}