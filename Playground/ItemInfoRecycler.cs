using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Playground;

internal class ItemInfoRecycler
{
    Stack<ItemInfo> pool = new Stack<ItemInfo>();
    Stack<ItemInfo> returnStack;
    bool postponeRecycling;

    public ItemInfoRecycler(bool postponeRecycling)
    {                
        this.postponeRecycling = postponeRecycling;
        returnStack = postponeRecycling ? new Stack<ItemInfo>() : pool;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnItemInfo(ItemInfo info)
    {                
        if (info == null) return;
        if (!postponeRecycling) info.Reset();
        returnStack.Push(info);                
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ItemInfo TakeItemInfo(ItemInfo parent, object objItem, ArraySegment<byte> itemName)
    {
        if (!pool.TryPop(out ItemInfo info)) info = new ItemInfo();
        info.Init(parent, objItem, itemName);
        return info;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetPooledItemInfos()
    {
        if (!postponeRecycling) return;
        foreach (var info in pool) info.Reset();
        foreach(var info in returnStack)                
        {
            info.Reset();
            pool.Push(info);
        }
        returnStack.Clear();
    }
}
