using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Lightweight, allocation-conscious helper that manages connections from a message source to sinks.
    /// Designed for hot paths to minimize memory and GC pressure. This type is a mutable struct on purpose.
    /// </summary>
    /// <remarks>
    /// IMPORTANT USAGE NOTES
    /// - This is a mutable struct that contains a lock. Do not copy it (e.g., assignment, boxing, capturing in lambdas).
    ///   Keep it as a field on a reference type (e.g., <see cref="SourceHelper"/>) and avoid passing it by value.
    /// - All sending/forwarding operations are lock-free for readers and may encounter invalidated weak references.
    ///   Such references are lazily cleaned up after the operation.
    /// - Connecting or disconnecting acquires an internal lock and compacts the internal array when necessary.
    ///
    /// MEMORY BEHAVIOR
    /// - Sinks are stored as <see cref="MessageSinkRef"/> entries, which can be strong or weak references. Weak references allow
    ///   sinks to be GC-collected without explicit disconnection.
    ///
    /// ORDERING
    /// - Synchronous forwarding calls sinks in index order (0..N-1).
    /// - Asynchronous forwarding awaits sinks sequentially in the same index order (0..N-1).
    /// - The async path optimizes for the case where all sinks complete synchronously, avoiding an async state machine.
    ///   If the first pending task is the last sink, that task is returned directly.
    ///
    /// SERIALIZATION
    /// - Fields are marked with <see cref="JsonIgnoreAttribute"/> and are not intended for serialization.
    /// </remarks>
    public struct SourceValueHelper
    {
        /// <summary>
        /// Storage of connected sinks (strong or weak). Exposed for performance only; treat as internal.
        /// </summary>
        [JsonIgnore]
        public MessageSinkRef[] sinks;

        /// <summary>
        /// Internal lock guarding mutations of <see cref="sinks"/> (connect/disconnect/compaction).
        /// Reads/forwards are intentionally lock-free.
        /// </summary>
        [JsonIgnore]
        private MicroValueLock changeLock;

        /// <summary>
        /// Returns a snapshot of currently connected and alive sinks.
        /// Invalid weak references are pruned lazily after the call if encountered.
        /// </summary>
        public IMessageSink[] GetConnectedSinks()
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return Array.Empty<IMessageSink>();

            bool anyInvalid = false;
            List<IMessageSink> resultList = new List<IMessageSink>(currentSinks.Length);

            for (int i = 0; i < currentSinks.Length; i++)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target)) resultList.Add(target);
                else anyInvalid = true;
            }

            if (anyInvalid) LockAndRemoveInvalidReferences();
            return resultList.ToArray();
        }

        /// <summary>
        /// Checks whether the specified sink is currently connected and alive.
        /// </summary>
        /// <remarks>O(n) scan over the current sink array.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsConnected(IMessageSink sink)
        {
            if (sink == null) return false;
            var currentSinks = this.sinks;
            if (currentSinks == null) return false;
            for (int i = 0; i < currentSinks.Length; i++)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target) && target == sink) return true;
            }
            return false;
        }

        /// <summary>
        /// Number of connected and alive sinks. Triggers lazy pruning when stale entries are detected.
        /// </summary>
        public int CountConnectedSinks
        {
            get
            {
                var currentSinks = this.sinks;
                if (currentSinks == null) return 0;

                int count = 0;
                for (int i = 0; i < currentSinks.Length; i++)
                {
                    if (currentSinks[i].IsValid) count++;
                }
                if (count != currentSinks.Length) LockAndRemoveInvalidReferences();

                return count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether there are no connected sinks.
        /// </summary>
        public bool NoConnectedSinks => this.sinks.EmptyOrNull();

        /// <summary>
        /// Forwards a message by reference to all currently connected sinks (0..N-1).
        /// Lock-free for readers; lazily prunes invalid sinks after the send, if encountered.
        /// Allocation-free on the hot path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Forward<M>(in M message)
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return;

            bool anyInvalid = false;
            int len = currentSinks.Length;
            for (int i = 0; i < len; i++)
            {
                bool ok = currentSinks[i].TryGetTarget(out IMessageSink target);
                if (ok) target.Post(in message);
                anyInvalid |= !ok;
            }
            if (anyInvalid) LockAndRemoveInvalidReferences();
        }

        /// <summary>
        /// Forwards a message by value to all currently connected sinks (0..N-1).
        /// Lock-free for readers; lazily prunes invalid sinks after the send, if encountered.
        /// Allocation-free on the hot path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Forward<M>(M message)
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return;

            bool anyInvalid = false;
            int len = currentSinks.Length;
            for (int i = 0; i < len; i++)
            {
                bool ok = currentSinks[i].TryGetTarget(out IMessageSink target);
                if (ok) target.Post(message);
                anyInvalid |= !ok;
            }
            if (anyInvalid) LockAndRemoveInvalidReferences();
        }

        /// <summary>
        /// Asynchronously forwards a message to all currently connected sinks in index order (0..N-1).
        /// </summary>
        /// <remarks>
        /// - If exactly one sink is alive, forwards directly to it.
        /// - For multiple sinks, awaits each sequentially in index order.
        /// - Optimized for the case where all PostAsync calls complete synchronously (no async state machine allocation).
        /// - If the first pending task is the last sink, that task is returned directly.
        /// - Lazily prunes invalid sinks after the send, if encountered.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task ForwardAsync<M>(M message)
        {
            var currentSinks = this.sinks;
            if (currentSinks == null) return Task.CompletedTask;

            if (currentSinks.Length == 1)
            {
                if (currentSinks[0].TryGetTarget(out IMessageSink target))
                {
                    return target.PostAsync(message);
                }
                else
                {
                    LockAndRemoveInvalidReferences();
                    return Task.CompletedTask;
                }
            }

            return MultipleForwardAsync(message, currentSinks);
        }

        /// <summary>
        /// Sends to multiple sinks sequentially in index order (0..N-1) to match synchronous forwarding.
        /// Avoids allocating an async state machine when all PostAsync calls complete synchronously.
        /// If the first pending task is the last sink, that task is returned directly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task MultipleForwardAsync<M>(M message, MessageSinkRef[] currentSinks)
        {
            bool anyInvalid = false;
            int len = currentSinks.Length;
            int lastIndex = len - 1;
            for (int i = 0; i < len; i++)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target))
                {
                    var task = target.PostAsync(message);
                    if (!IsCompletedSuccessfully(task))
                    {
                        if (i == lastIndex)
                        {
                            if (anyInvalid) LockAndRemoveInvalidReferences(); // ensure pruning still happens
                            return task; // no state machine allocation
                        }
                        // Switch to async path only when needed.
                        return AwaitRemainingAsync(message, currentSinks, i + 1, anyInvalid, task);
                    }
                }
                else anyInvalid = true;
            }

            if (anyInvalid) LockAndRemoveInvalidReferences();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Continues awaiting sequentially after encountering the first non-synchronously-completed task.
        /// Preserves ordering and backpressure semantics.
        /// </summary>
        private async Task AwaitRemainingAsync<M>(M message, MessageSinkRef[] currentSinks, int nextIndex, bool anyInvalid, Task firstPending)
        {
            await firstPending.ConfiguredAwait();

            int len = currentSinks.Length;
            for (int i = nextIndex; i < len; i++)
            {
                if (currentSinks[i].TryGetTarget(out IMessageSink target))
                {
                    await target.PostAsync(message).ConfiguredAwait();
                }
                else anyInvalid = true;
            }

            if (anyInvalid) LockAndRemoveInvalidReferences();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCompletedSuccessfully(Task task)
        {
            return task.Status == TaskStatus.RanToCompletion;
        }

        /// <summary>
        /// Acquires the lock and compacts sink references if invalid entries exist.
        /// Marked NoInlining intentionally (cold path) to keep hot loops small and I-cache friendly.
        /// Compaction affects subsequent sends; the current forwarding loop operates on its snapshot.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void LockAndRemoveInvalidReferences()
        {
            if (sinks == null) return;
            changeLock.Enter();
            try
            {
                RemoveInvalidReferences();
            }
            finally
            {
                changeLock.Exit();
            }
        }

        /// <summary>
        /// NOTE: Lock must already be acquired!
        /// Compacts the sinks array by removing invalid (collected) weak references (copy-on-write).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveInvalidReferences()
        {
            var local = sinks;
            if (local == null) return;

            // Fast path when all valid or empty
            int count = 0;
            for (int i = 0; i < local.Length; i++)
            {
                if (local[i].IsValid) count++;
            }

            if (count == local.Length) return;
            if (count == 0)
            {
                sinks = null;
                return;
            }

            var newSinks = new MessageSinkRef[count];
            int idx = 0;
            for (int i = 0; i < local.Length; i++)
            {
                if (local[i].IsValid) newSinks[idx++] = local[i];
            }
            sinks = newSinks;
        }

        /// <summary>
        /// Connects a sink. When <paramref name="weakReference"/> is true, keeps a weak reference so the sink can be GC-collected.
        /// </summary>
        /// <remarks>
        /// Acquires the internal lock and compacts the array before appending to reduce future pruning work.
        /// Uses copy-on-write growth to keep readers safe.
        /// </remarks>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            if (sink == null) return;

            changeLock.Enter();
            try
            {
                if (sinks == null)
                {
                    sinks = new MessageSinkRef[] { new MessageSinkRef(sink, weakReference) };
                }
                else
                {
                    // Keep array compact before appending to reduce churn later.
                    RemoveInvalidReferences();

                    if (sinks == null)
                    {
                        sinks = new MessageSinkRef[] { new MessageSinkRef(sink, weakReference) };
                    }
                    else
                    {
                        MessageSinkRef[] newSinks = new MessageSinkRef[sinks.Length + 1];
                        Array.Copy(sinks, 0, newSinks, 0, sinks.Length);
                        newSinks[newSinks.Length - 1] = new MessageSinkRef(sink, weakReference);
                        sinks = newSinks;
                    }
                }
            }
            finally
            {
                changeLock.Exit();
            }
        }

        /// <summary>
        /// Connects a bidirectional flow element. Returns the same element typed as a source to enable fluent chaining.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            ConnectTo(sink as IMessageSink, weakReference);
            return sink;
        }

        /// <summary>
        /// Disconnects all currently connected sinks.
        /// </summary>
        public void DisconnectAll()
        {
            if (sinks == null) return;

            changeLock.Enter();
            try
            {
                sinks = null;
            }
            finally
            {
                changeLock.Exit();
            }
        }

        /// <summary>
        /// Disconnects the specified sink if connected.
        /// Uses copy-on-write removal to keep readers safe.
        /// </summary>
        public void DisconnectFrom(IMessageSink sink)
        {
            if (sinks == null) return;

            changeLock.Enter();
            try
            {
                if (sinks.Length == 1)
                {
                    if (!sinks[0].TryGetTarget(out IMessageSink target) || target == sink)
                    {
                        sinks = null;
                    }
                }
                else
                {
                    int? index = null;
                    for (int i = 0; i < sinks.Length; i++)
                    {
                        if (sinks[i].TryGetTarget(out IMessageSink target) && target == sink)
                        {
                            index = i;
                            break;
                        }
                    }
                    if (!index.HasValue) return;

                    MessageSinkRef[] newSinks = new MessageSinkRef[sinks.Length - 1];
                    Array.Copy(sinks, 0, newSinks, 0, index.Value);
                    Array.Copy(sinks, index.Value + 1, newSinks, index.Value, newSinks.Length - index.Value);
                    sinks = newSinks;
                }
            }
            finally
            {
                changeLock.Exit();
            }
        }
    }
}