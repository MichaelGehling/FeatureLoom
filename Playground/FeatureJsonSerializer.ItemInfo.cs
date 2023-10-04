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
            string itemName_str;

            public void Init(ItemInfo parentInfo, object objItem, byte[] itemName)
            {
                this.parentInfo = parentInfo;
                this.objItem = objItem;
                this.itemName = itemName;
                this.itemName_str = null;
            }

            public void Init(ItemInfo parentInfo, object objItem, string itemName)
            {
                this.parentInfo = parentInfo;
                this.objItem = objItem;
                this.itemName = null;
                this.itemName_str = itemName;
            }

            public byte[] ItemName
            {
                get
                {
                    if (itemName == null) itemName = JsonUTF8StreamWriter.PreparePrimitiveToBytes(itemName_str);
                    return itemName;
                }                
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                parentInfo = null;
                objItem = null;
                itemName = null;
                itemName_str = null;
            }
        }

    }
}
