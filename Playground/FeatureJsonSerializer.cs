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
        MicroValueLock serializerLock = new MicroValueLock();        
        LazyValue<MemoryStream> memoryStream = new();
        JsonUTF8StreamWriter writer;
        readonly CompiledSettings settings;
        Dictionary<Type, CachedTypeHandler> typeHandlerCache = new();
        Dictionary<Type, CachedKeyWriter> keyWriterCache = new();
        Dictionary<object, ItemInfo> objToItemInfo = new();
        ItemInfoRecycler itemInfoRecycler;
        private ArraySegment<byte> rootName;
        ItemInfo currentItemInfo;
        ExtensionApi extensionApi;
        public delegate void ItemHandler<T>(T item);
        public delegate bool TryCreateItemHandlerDelegate<T>(ExtensionApi api, out ItemHandler<T> itemHandler, out JsonDataTypeCategory category);

        public FeatureJsonSerializer(Settings settings = null)
        {           
            this.settings = new CompiledSettings(settings ?? new Settings());
            writer = new JsonUTF8StreamWriter(this.settings);
            itemInfoRecycler = new ItemInfoRecycler(this);
            rootName = new ArraySegment<byte>(writer.PrepareRootName());
            this.extensionApi = new ExtensionApi(this);
        }
        void FinishSerialization()
        {
            writer.ResetBuffer();
            if (memoryStream.Exists) memoryStream.Obj.Position = 0;
            writer.stream = null;
            if (objToItemInfo.Count > 0) objToItemInfo.Clear();
            itemInfoRecycler.ResetPooledItemInfos();            
        }

        CachedTypeHandler lastTypeHandler = null;
        Type lastTypeHandlerType = null;

        public string Serialize<T>(T item)
        {
            serializerLock.Enter();
            try
            {
                writer.stream = memoryStream.Obj;

                if (item == null)
                {
                    return "null";
                }

                Type itemType = item.GetType();

                if (lastTypeHandlerType == itemType)
                {                    
                    lastTypeHandler.HandleItem(item, rootName);                    
                }
                else
                {
                    var typeHandler = GetCachedTypeHandler(itemType);
                    
                    typeHandler.HandleItem(item, rootName);                    

                    lastTypeHandler = typeHandler;
                    lastTypeHandlerType = typeHandler.HandlerType;
                }

                if (memoryStream.Obj.Length == 0)
                {
                    return Encoding.UTF8.GetString(writer.Buffer, 0, writer.BufferCount);
                }
                else
                {
                    writer.WriteBufferToStream();
                    return Encoding.UTF8.GetString(memoryStream.Obj.GetBuffer(), 0, (int)memoryStream.Obj.Length);
                }                                        
            }
            finally
            {
                FinishSerialization();
                serializerLock.Exit();
            }
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateItemInfoForClass<T>(T item, ArraySegment<byte> itemName)
        {            
            currentItemInfo = itemInfoRecycler.TakeItemInfo(currentItemInfo, item, itemName);            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateItemInfoForStruct(ArraySegment<byte> itemName)
        {
            currentItemInfo = itemInfoRecycler.TakeItemInfo(currentItemInfo, null, itemName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UseParentItemInfo()
        {
            if (currentItemInfo == null) return;
            var parentItemInfo = currentItemInfo.parentInfo;
            itemInfoRecycler.ReturnItemInfo(currentItemInfo);
            currentItemInfo = parentItemInfo;
        }

        public void Serialize<T>(Stream stream, T item)
        {
            serializerLock.Enter();
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
                    lastTypeHandler.HandleItem(item, rootName);                    
                }
                else
                {
                    var typeHandler = GetCachedTypeHandler(itemType);
                    
                    typeHandler.HandleItem(item, rootName);                    

                    lastTypeHandler = typeHandler;
                    lastTypeHandlerType = typeHandler.HandlerType;
                }

                writer.WriteBufferToStream();
            }
            finally
            {
                FinishSerialization();
                serializerLock.Exit();
            }
        
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetCachedKeyWriter(Type itemType, out CachedKeyWriter stringValueWriter)
        {
            return keyWriterCache.TryGetValue(itemType, out stringValueWriter) || 
                   TryCreateKeyWriter(itemType, out stringValueWriter);
        }

        private bool TryCreateKeyWriter(Type itemType, out CachedKeyWriter stringValueWriter)
        {
            stringValueWriter = new(!settings.requiresItemNames);

            if (itemType == typeof(string)) stringValueWriter.SetWriterMethod<string>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(bool)) stringValueWriter.SetWriterMethod<bool>(writer.WriteBoolAsStringValue);
            else if (itemType == typeof(char)) stringValueWriter.SetWriterMethod<char>(writer.WriteCharValueAsString);
            else if (itemType == typeof(sbyte)) stringValueWriter.SetWriterMethod<sbyte>(writer.WriteSbyteValueAsString);
            else if (itemType == typeof(short)) stringValueWriter.SetWriterMethod<short>(writer.WriteShortValueAsString);
            else if (itemType == typeof(int)) stringValueWriter.SetWriterMethod<int>(writer.WriteIntValueAsString);
            else if (itemType == typeof(long)) stringValueWriter.SetWriterMethod<long>(writer.WriteLongValueAsString);
            else if (itemType == typeof(byte)) stringValueWriter.SetWriterMethod<byte>(writer.WriteByteAsStringValue);
            else if (itemType == typeof(ushort)) stringValueWriter.SetWriterMethod<ushort>(writer.WriteUshortValueAsString);
            else if (itemType == typeof(uint)) stringValueWriter.SetWriterMethod<uint>(writer.WriteUintValueAsString);
            else if (itemType == typeof(ulong)) stringValueWriter.SetWriterMethod<ulong>(writer.WriteUlongValueAsString);
            else if (itemType == typeof(Guid)) stringValueWriter.SetWriterMethod<Guid>(value => writer.WritePrimitiveValueAsString(value.ToString()));
            else if (itemType == typeof(DateTime)) stringValueWriter.SetWriterMethod<DateTime>(value => writer.WritePrimitiveValueAsString(value.ToString()));

            if (itemType == typeof(string)) stringValueWriter.SetWriterMethod<string>(writer.WriteStringValueAsStringWithCopy);
            else if (itemType == typeof(bool)) stringValueWriter.SetWriterMethod<bool>(writer.WriteBoolValueAsStringWithCopy);
            else if (itemType == typeof(char)) stringValueWriter.SetWriterMethod<char>(writer.WriteCharValueAsStringWithCopy);
            else if (itemType == typeof(sbyte)) stringValueWriter.SetWriterMethod<sbyte>(writer.WriteSbyteValueAsStringWithCopy);
            else if (itemType == typeof(short)) stringValueWriter.SetWriterMethod<short>(writer.WriteShortValueAsStringWithCopy);
            else if (itemType == typeof(int)) stringValueWriter.SetWriterMethod<int>(writer.WriteIntValueAsStringWithCopy);
            else if (itemType == typeof(long)) stringValueWriter.SetWriterMethod<long>(writer.WriteLongValueAsStringWithCopy);
            else if (itemType == typeof(byte)) stringValueWriter.SetWriterMethod<byte>(writer.WriteByteValueAsStringWithCopy);
            else if (itemType == typeof(ushort)) stringValueWriter.SetWriterMethod<ushort>(writer.WriteUshortValueAsStringWithCopy);
            else if (itemType == typeof(uint)) stringValueWriter.SetWriterMethod<uint>(writer.WriteUintValueAsStringWithCopy);
            else if (itemType == typeof(ulong)) stringValueWriter.SetWriterMethod<ulong>(writer.WriteUlongValueAsStringWithCopy);
            else if (itemType == typeof(Guid)) stringValueWriter.SetWriterMethod<Guid>(value => writer.WriteStringValueAsStringWithCopy(value.ToString()));
            else if (itemType == typeof(DateTime)) stringValueWriter.SetWriterMethod<DateTime>(value => writer.WriteStringValueAsStringWithCopy(value.ToString()));

            return stringValueWriter.HasMethod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CachedTypeHandler GetCachedTypeHandler(Type itemType)
        {
            return typeHandlerCache.TryGetValue(itemType, out var cachedTypeHandler) ? cachedTypeHandler : CreateCachedTypeHandler(itemType);
        }


        private CachedTypeHandler CreateCachedTypeHandler(Type itemType)
        {
            CachedTypeHandler typeHandler = new CachedTypeHandler(this);
            typeHandlerCache[itemType] = typeHandler; // Typehandler must be added first for the case of recursion (type contains same type)

            typeHandler.preparedTypeInfo = writer.PrepareTypeInfo(itemType.GetSimplifiedTypeName());

            foreach(var creator in settings.itemHandlerCreators)
            {
                if (!creator.SupportsType(itemType)) continue;

                creator.CreateTypeHandler(extensionApi, typeHandler, itemType);
                return typeHandler;
            }


            if (itemType == typeof(int)) typeHandler.SetItemHandler_Primitive<int>(writer.WriteIntValue);            
            else if (itemType == typeof(uint)) typeHandler.SetItemHandler_Primitive<uint>(writer.WriteUintValue);
            else if (itemType == typeof(long)) typeHandler.SetItemHandler_Primitive<long>(writer.WriteLongValue);
            else if (itemType == typeof(ulong)) typeHandler.SetItemHandler_Primitive<ulong>(writer.WriteUlongValue);
            else if (itemType == typeof(short)) typeHandler.SetItemHandler_Primitive<short>(writer.WriteShortValue);
            else if (itemType == typeof(ushort)) typeHandler.SetItemHandler_Primitive<ushort>(writer.WriteUshortValue);
            else if (itemType == typeof(sbyte)) typeHandler.SetItemHandler_Primitive<sbyte>(writer.WriteSbyteValue);
            else if (itemType == typeof(byte)) typeHandler.SetItemHandler_Primitive<byte>(writer.WriteByteValue);
            else if (itemType == typeof(string)) typeHandler.SetItemHandler_Primitive<string>(writer.WriteStringValue);
            else if (itemType == typeof(float)) typeHandler.SetItemHandler_Primitive<float>(writer.WriteFloatValue);
            else if (itemType == typeof(double)) typeHandler.SetItemHandler_Primitive<double>(writer.WriteDoubleValue);
            else if (itemType == typeof(decimal)) typeHandler.SetItemHandler_Primitive<decimal>(writer.WriteDecimalValue);
            else if (itemType == typeof(char)) typeHandler.SetItemHandler_Primitive<char>(writer.WriteCharValue);
            else if (itemType == typeof(bool)) typeHandler.SetItemHandler_Primitive<bool>(writer.WriteBoolValue);
            else if (itemType == typeof(IntPtr)) typeHandler.SetItemHandler_Primitive<IntPtr>(writer.WriteIntPtrValue);
            else if (itemType == typeof(UIntPtr)) typeHandler.SetItemHandler_Primitive<UIntPtr>(writer.WriteUintPtrValue);
            else if (itemType == typeof(Guid)) typeHandler.SetItemHandler_Primitive<Guid>(writer.WriteGuidValue);
            else if (itemType == typeof(DateTime)) typeHandler.SetItemHandler_Primitive<DateTime>(writer.WriteDateTimeValue);
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
   

        private void CreateEnumItemHandler<T>(CachedTypeHandler typeHandler) where T : struct, Enum
        {
            if (settings.enumAsString)
            {
                typeHandler.SetItemHandler_Primitive<T>(item => writer.WriteStringValue(item.ToName()));
            }
            else
            {
                typeHandler.SetItemHandler_Primitive<T>(item => writer.WriteIntValue(item.ToInt()));
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void StartTypeInfoObject(byte[] preparedTypeInfo)
        {
            writer.OpenObject();
            writer.WriteToBuffer(preparedTypeInfo);
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
