using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    /// Used to store the reference of a connected IDataFlowSink.
    /// Can either use a strong reference (default) or a weak reference.
    /// The weak reference will not keep the referenced object alive when it is not referenced
    /// somewhere else, so the garbage collector can remove it though it is connected
    /// via the Dataflow.
    /// </summary>
    public readonly struct DataFlowReference
    {
        private readonly IDataFlowSink strongRefSink;
        private readonly WeakReference<IDataFlowSink> weakRefSink;

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