using FeatureLoom.Helpers;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        class ItemInfoRecycler
        {
            Stack<ItemInfo> pool = new Stack<ItemInfo>();
            Stack<ItemInfo> returnStack;
            bool postponeRecycling;

            public ItemInfoRecycler(FeatureJsonSerializer serializer)
            {                
                postponeRecycling = serializer.settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef;
                returnStack = postponeRecycling ? new Stack<ItemInfo>() : pool;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ReturnItemInfo(ItemInfo info)
            {                
                if (info == null) return;
                info.Reset(!postponeRecycling);
                returnStack.Push(info);                
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ItemInfo TakeItemInfo(ItemInfo parent, object objItem, byte[] itemName)
            {
                if (!pool.TryPop(out ItemInfo info)) info = new ItemInfo();
                info.Init(parent, objItem, itemName);
                return info;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ItemInfo TakeItemInfo(ItemInfo parent, object objItem, SlicedBuffer<byte>.Slice itemName)
            {
                if (!pool.TryPop(out ItemInfo info)) info = new ItemInfo();
                info.Init(parent, objItem, itemName);
                return info;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ResetPooledItemInfos()
            {
                if (!postponeRecycling) return;
                foreach (var info in pool) info.Reset(false);
                foreach(var info in returnStack)
                //while(returnStack.TryPop(out var info))
                {
                    info.Reset(false);
                    pool.Push(info);
                }
                returnStack.Clear();
            }
        }

    }
}
