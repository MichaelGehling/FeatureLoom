using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.DataFlows
{
    public struct DataFlowReference
    {
        readonly IDataFlowSink strongRefSink;
        readonly WeakReference<IDataFlowSink> weakRefSink;

        public DataFlowReference(IDataFlowSink sink, bool weakReference)
        {
            weakRefSink = weakReference ? new WeakReference<IDataFlowSink>(sink) : null;
            strongRefSink = weakReference ? null : sink;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetTarget(out IDataFlowSink sink)
        {
            if (weakRefSink == null)
            {                
                sink = strongRefSink;
                return sink != null;
            }
            else
            {
                return weakRefSink.TryGetTarget(out sink);
            }
        }

    }
}