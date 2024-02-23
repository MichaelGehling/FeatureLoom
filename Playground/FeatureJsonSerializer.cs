using FeatureLoom.Synchronization;
using FeatureLoom.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Reflection.Metadata;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using FeatureLoom.Helpers;
using FeatureLoom.Collections;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        FeatureLock serializerLock = new FeatureLock();
        Dictionary<object, ItemInfo> objToItemInfo = new();
        MemoryStream memoryStream = new MemoryStream();
        JsonUTF8StreamWriter writer = new JsonUTF8StreamWriter();
        readonly CompiledSettings settings;
        Dictionary<Type, CachedTypeHandler> typeHandlerCache = new();
        Dictionary<Type, CachedStringValueWriter> stringValueWriterCache = new();

        delegate void ItemHandler<T>(T item, Type expectedType, ItemInfo itemInfo);
        delegate void PrimitiveItemHandler<T>(T item);

        ItemInfoRecycler itemInfoRecycler;

        public FeatureJsonSerializer(Settings settings = null)
        {
            this.settings = new CompiledSettings(settings ?? new Settings());

            itemInfoRecycler = new ItemInfoRecycler(this);
        }
        void FinishSerialization()
        {
            writer.ResetBuffer();
            memoryStream.Position = 0;
            writer.stream = null;
            if (objToItemInfo.Count > 0) objToItemInfo.Clear();

            if (settings.typeInfoHandling != TypeInfoHandling.AddNoTypeInfo) itemInfoRecycler.ResetPooledItemInfos();
        }

        CachedTypeHandler lastTypeHandler = null;
        Type lastTypeHandlerType = null;

        public string Serialize<T>(T item)
        {
            using (serializerLock.Lock())
            {
                try
                {
                    writer.stream = memoryStream;

                    if (item == null)
                    {
                        return "null";
                    }
                    Type itemType = item.GetType();

                    if (lastTypeHandlerType == itemType)
                    {
                        ItemInfo itemInfo = CreateItemInfo(item, null, JsonUTF8StreamWriter.ROOT);
                        lastTypeHandler.HandleItem(item, itemInfo);
                    }
                    else
                    {
                        var typeHandler = GetCachedTypeHandler(itemType);

                        ItemInfo itemInfo = CreateItemInfo(item, null, JsonUTF8StreamWriter.ROOT);
                        typeHandler.HandleItem(item, itemInfo);

                        lastTypeHandler = typeHandler;
                        lastTypeHandlerType = typeHandler.HandlerType;
                    }

                    if (memoryStream.Length == 0)
                    {
                        return Encoding.UTF8.GetString(writer.Buffer, 0, writer.BufferCount);
                    }
                    else
                    {
                        writer.WriteBufferToStream();
                        return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                    }                                        
                }
                finally
                {
                    FinishSerialization();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ItemInfo CreateItemInfo<T>(T item, ItemInfo parentinfo, byte[] itemName)
        {
            if (!settings.requiresItemInfos) return null;
            if (typeof(T).IsClass) return itemInfoRecycler.TakeItemInfo(parentinfo, item, itemName);
            else return itemInfoRecycler.TakeItemInfo(parentinfo, null, itemName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ItemInfo CreateItemInfo<T>(T item, ItemInfo parentinfo, string itemName)
        {
            if (!settings.requiresItemInfos) return null;
            if (typeof(T).IsClass) return itemInfoRecycler.TakeItemInfo(parentinfo, item, itemName);
            else return itemInfoRecycler.TakeItemInfo(parentinfo, null, itemName);
        }

        public void Serialize<T>(Stream stream, T item)
        {
            using (serializerLock.Lock())
            {
                try
                {
                    writer.stream = stream;

                    if (item == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type itemType = item.GetType();

                    if (lastTypeHandlerType == itemType)
                    {
                        ItemInfo itemInfo = CreateItemInfo(item, null, JsonUTF8StreamWriter.ROOT);
                        lastTypeHandler.HandleItem(item, itemInfo);
                    }
                    else
                    {
                        var typeHandler = GetCachedTypeHandler(itemType);

                        ItemInfo itemInfo = CreateItemInfo(item, null, JsonUTF8StreamWriter.ROOT);
                        typeHandler.HandleItem(item, itemInfo);

                        lastTypeHandler = typeHandler;
                        lastTypeHandlerType = typeHandler.HandlerType;
                    }

                    writer.WriteBufferToStream();
                }
                finally
                {
                    FinishSerialization();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetCachedStringValueWriter(Type itemType, out CachedStringValueWriter stringValueWriter)
        {
            return stringValueWriterCache.TryGetValue(itemType, out stringValueWriter) || 
                   TryCreateStringValueWriter(itemType, out stringValueWriter);
        }

        private bool TryCreateStringValueWriter(Type itemType, out CachedStringValueWriter stringValueWriter)
        {
            stringValueWriter = new();

            if (itemType == typeof(string)) stringValueWriter.SetWriterMethod<string>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(bool)) stringValueWriter.SetWriterMethod<bool>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(char)) stringValueWriter.SetWriterMethod<char>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(sbyte)) stringValueWriter.SetWriterMethod<sbyte>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(short)) stringValueWriter.SetWriterMethod<short>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(int)) stringValueWriter.SetWriterMethod<int>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(long)) stringValueWriter.SetWriterMethod<long>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(byte)) stringValueWriter.SetWriterMethod<byte>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(ushort)) stringValueWriter.SetWriterMethod<ushort>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(uint)) stringValueWriter.SetWriterMethod<uint>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(ulong)) stringValueWriter.SetWriterMethod<ulong>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(Guid)) stringValueWriter.SetWriterMethod<Guid>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(DateTime)) stringValueWriter.SetWriterMethod<DateTime>(writer.WritePrimitiveValueAsString);

            return stringValueWriter.HasMethod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CachedTypeHandler GetCachedTypeHandler(Type itemType)
        {
            return typeHandlerCache.TryGetValue(itemType, out var typeCacheItem) ? typeCacheItem : CreateCachedTypeHandler(itemType);
        }

        private CachedTypeHandler CreateCachedTypeHandler(Type itemType)
        {
            CachedTypeHandler typeHandler = new CachedTypeHandler(this);
            typeHandlerCache[itemType] = typeHandler;

            typeHandler.preparedTypeInfo = writer.PrepareTypeInfo(itemType.GetSimplifiedTypeName());

            if (itemType == typeof(int)) CreatePrimitiveItemHandler<int>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(uint)) CreatePrimitiveItemHandler<uint>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(long)) CreatePrimitiveItemHandler<long>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(ulong)) CreatePrimitiveItemHandler<ulong>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(short)) CreatePrimitiveItemHandler<short>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(ushort)) CreatePrimitiveItemHandler<ushort>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(sbyte)) CreatePrimitiveItemHandler<sbyte>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(byte)) CreatePrimitiveItemHandler<byte>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(string)) CreatePrimitiveItemHandler<string>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(float)) CreatePrimitiveItemHandler<float>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(double)) CreatePrimitiveItemHandler<double>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(char)) CreatePrimitiveItemHandler<char>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(IntPtr)) CreatePrimitiveItemHandler<IntPtr>(typeHandler, writer.WritePrimitiveValue); //Make specialized
            else if (itemType == typeof(UIntPtr)) CreatePrimitiveItemHandler<UIntPtr>(typeHandler, writer.WritePrimitiveValue); //Make specialized
            else if (itemType == typeof(Guid)) CreatePrimitiveItemHandler<Guid>(typeHandler, writer.WritePrimitiveValue); //Make specialized
            else if (itemType == typeof(DateTime)) CreatePrimitiveItemHandler<DateTime>(typeHandler, writer.WritePrimitiveValue); //Make specialized
            else if (itemType.IsEnum) CreateAndSetItemHandlerViaReflection(typeHandler, itemType, nameof(CreateEnumItemHandler), true);
            else if (TryCreateDictionaryItemHandler(typeHandler, itemType)) /* do nothing */;
            else if (TryCreateListItemHandler(typeHandler, itemType)) /* do nothing */;
            else if (TryCreateEnumerableItemHandler(typeHandler, itemType)) /* do nothing */;
            else CreateComplexItemHandler(typeHandler, itemType);
            
            
            return typeHandler;

            void CreateAndSetItemHandlerViaReflection(CachedTypeHandler typeHandler, Type itemType, string getItemHandlerMethodName, bool isPrimitive)
            {
                MethodInfo method = typeof(FeatureJsonSerializer).GetMethod(getItemHandlerMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(itemType);
                generic.Invoke(this, new object[] { typeHandler });
            }
        }

        private void CreatePrimitiveItemHandler<T>(CachedTypeHandler typeHandler, PrimitiveItemHandler<T> write)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeHandler.SetItemHandler<T>((item) =>
                {
                    StartTypeInfoObject(typeHandler.preparedTypeInfo);
                    write(item);
                    FinishTypeInfoObject();
                });
            }
            else
            {
                typeHandler.SetItemHandler(write);
            }
        }        

        private void CreateEnumItemHandler<T>(CachedTypeHandler typeHandler) where T : struct, Enum
        {
            if (settings.enumAsString)
            {                
                CreatePrimitiveItemHandler<T>(typeHandler, item => writer.WritePrimitiveValue(item.ToName()));
            }
            else
            {
                CreatePrimitiveItemHandler<T>(typeHandler, item => writer.WritePrimitiveValue(item.ToInt()));
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void StartTypeInfoObject(byte[] preparedTypeInfo)
        {
            writer.OpenObject();
            writer.WritePreparedByteString(preparedTypeInfo);
            writer.WriteComma();
            writer.WriteValueFieldName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FinishTypeInfoObject()
        {
            writer.CloseObject();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryHandleItemAsRef<T>(T item, ItemInfo itemInfo, Type itemType)
        {
            if (settings.referenceCheck == ReferenceCheck.NoRefCheck || itemInfo == null || item == null || !itemType.IsClass) return false;
            return TryHandleObjAsRef(item, itemInfo, itemType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryHandleObjAsRef(object obj, ItemInfo itemInfo, Type itemType)
        {
            if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef)
            {
                if (!objToItemInfo.TryAdd(obj, itemInfo))
                {
                    writer.WriteRefObject(objToItemInfo[obj]);
                    return true;
                }
            }
            else
            {
                itemInfo = itemInfo.parentInfo;
                while (itemInfo != null)
                {
                    if (itemInfo.objItem == obj)
                    {
                        if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByRef) writer.WriteRefObject(itemInfo);
                        else if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByNull) writer.WriteNullValue();
                        else if (settings.referenceCheck == ReferenceCheck.OnLoopThrowException) throw new Exception("Circular referencing detected!");
                        return true;
                    }
                    itemInfo = itemInfo.parentInfo;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TypeInfoRequired(Type actualType, Type expectedType)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo) return true;
            if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && actualType != expectedType) return true;
            return false;
        }

    }
}
