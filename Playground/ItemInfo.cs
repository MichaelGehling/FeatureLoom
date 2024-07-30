using FeatureLoom.Helpers;
using System;
using System.Runtime.CompilerServices;

namespace Playground
{
    internal class ItemInfo
    {
        public ItemInfo parentInfo;
        public object objItem;
        ArraySegment<byte> itemName;

        public void Init(ItemInfo parentInfo, object objItem, ArraySegment<byte> itemName)
        {
            this.parentInfo = parentInfo;
            this.objItem = objItem;
            this.itemName = itemName;
        }

        public ArraySegment<byte> ItemName
        {
            get
            {
                return itemName;                    
            }                
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            parentInfo = null;
            objItem = null;
            itemName = default;
        }
    }
}
