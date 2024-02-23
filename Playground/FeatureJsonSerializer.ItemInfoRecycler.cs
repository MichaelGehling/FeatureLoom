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

            public ItemInfoRecycler(FeatureJsonSerializer serializer)
            {                
                bool postponeRecycling = serializer.settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef;
                returnStack = postponeRecycling ? new Stack<ItemInfo>() : pool;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ItemInfo TakeItemInfo(ItemInfo parent, object objItem, byte[] itemName)
            {
                if (!pool.TryPop(out ItemInfo info)) info = new ItemInfo();
                info.Init(parent, objItem, itemName);
                returnStack.Push(info);
                return info;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ItemInfo TakeItemInfo(ItemInfo parent, object objItem, string itemName)
            {
                if (!pool.TryPop(out ItemInfo info)) info = new ItemInfo();
                info.Init(parent, objItem, itemName);
                returnStack.Push(info);
                return info;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ResetPooledItemInfos()
            {
                foreach (var info in pool) info.Reset();
                if (pool == returnStack) return;
                while(returnStack.TryPop(out var info))
                {
                    info.Reset();
                    pool.Push(info);
                }
            }
        }

    }
}
