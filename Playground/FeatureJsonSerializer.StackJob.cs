using System;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        abstract class StackJob
        {
            public object objItem;
            public StackJob parentJob;
            public byte[] itemName;
            public IStackJobRecycler recycler;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Recycle() => recycler.RecycleJob(this);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public abstract bool Process();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public virtual void Reset()
            {
                parentJob = null;
                objItem = null;
                itemName = null;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public abstract byte[] GetCurrentChildItemName();
        }

        class RefJob : StackJob
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override byte[] GetCurrentChildItemName()
            {
                throw new NotImplementedException();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Process()
            {
                throw new NotImplementedException();
            }
        }

    }
}
