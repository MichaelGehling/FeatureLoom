using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Used to store the reference of a connected IMessageSink.
    /// Can either use a strong reference (default) or a weak reference.
    /// The weak reference will not keep the referenced object alive when it is not referenced
    /// somewhere else, so the garbage collector can remove it though it is connected
    /// via the MessageFlow.
    /// </summary>
    public readonly struct MessageSinkRef
    {
        private readonly IMessageSink strongRefSink;
        private readonly WeakReference<IMessageSink> weakRefSink;

        public MessageSinkRef(IMessageSink sink, bool weakReference)
        {
            weakRefSink = weakReference ? new WeakReference<IMessageSink>(sink) : null;
            strongRefSink = weakReference ? null : sink;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetTarget(out IMessageSink sink)
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