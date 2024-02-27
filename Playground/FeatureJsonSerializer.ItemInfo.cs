using FeatureLoom.Helpers;
using System;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        class ItemInfo
        {
            public ItemInfo parentInfo;
            public object objItem;
            byte[] itemName;
            SlicedBuffer<byte>.Slice itemName_slice;

            public void Init(ItemInfo parentInfo, object objItem, byte[] itemName)
            {
                this.parentInfo = parentInfo;
                this.objItem = objItem;
                this.itemName = itemName;
                this.itemName_slice = default;
            }

            public void Init(ItemInfo parentInfo, object objItem, SlicedBuffer<byte>.Slice itemName)
            {
                this.parentInfo = parentInfo;
                this.objItem = objItem;
                this.itemName = null;
                this.itemName_slice = itemName;
            }

            public ArraySegment<byte> ItemName
            {
                get
                {
                    if (itemName == null) return itemName_slice.AsArraySegment();
                    return new ArraySegment<byte>(itemName);
                }                
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset(bool disposeSlice)
            {
                parentInfo = null;
                objItem = null;
                itemName = null;
                if (disposeSlice) itemName_slice.Dispose();
            }
        }

    }
}
