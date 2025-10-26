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
            if (weakReference)
            {
                weakRefSink = new WeakReference<IMessageSink>(sink);
            }
            else
            {
                strongRefSink = sink;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetTarget(out IMessageSink sink)
        {
            sink = strongRefSink;
            if (sink != null) return true;
            if (weakRefSink != null) return weakRefSink.TryGetTarget(out sink);
            return false;
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (strongRefSink != null) return true;
                if (weakRefSink == null) return false;
                return weakRefSink.TryGetTarget(out _);
            }
        }
    }
}