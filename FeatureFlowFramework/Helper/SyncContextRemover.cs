using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.Helper
{
    public struct SyncContextRemover : INotifyCompletion
    {
        public bool IsCompleted => SynchronizationContext.Current == null;

        public void OnCompleted(Action continuation)
        {
            var prevContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                continuation();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        public SyncContextRemover GetAwaiter()
        {
            return this;
        }

        public void GetResult()
        {
        }
    }
}