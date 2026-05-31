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
using System.Reflection;
using FeatureLoom.Helpers;
using FeatureLoom.Collections;

namespace FeatureLoom.Serialization
{
    public sealed partial class JsonSerializer
    {
        MicroValueLock serializerLock = new MicroValueLock();        
        LazyUnsafeValue<MemoryStream> memoryStream = new();
        readonly JsonUTF8StreamWriter writer;
        readonly CompiledSettings settings;
        readonly Dictionary<Type, CachedTypeWriter> typeWriterCache = new();
        readonly Dictionary<Type, CachedKeyWriter> keyWriterCache = new();
        readonly Dictionary<object, ItemInfo> objToItemInfo = new();
        readonly ItemInfoRecycler itemInfoRecycler;
        private ByteSegment rootName;
        ItemInfo currentItemInfo;
        readonly ExtensionApi extensionApi;
        public delegate void ItemHandler<T>(T item);
        public delegate bool TryCreateItemHandlerDelegate<T>(ExtensionApi api, out ItemHandler<T> itemHandler, out JsonDataTypeCategory category);

        public JsonSerializer(Settings settings = null)
        {           
            this.settings = new CompiledSettings(settings ?? new Settings());
            writer = new JsonUTF8StreamWriter(this.settings);
            itemInfoRecycler = new ItemInfoRecycler(this.settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef);
            rootName = new ByteSegment(writer.PrepareRootName());
            this.extensionApi = new ExtensionApi(this);
        }

        public string ShowBufferAsString()
        {            
            ByteSegment segment = new ByteSegment(writer.Buffer, 0, writer.BufferCount);
            return segment.ToString();
        }


        void FinishSerialization()
        {
            writer.ResetBuffer();
            if (memoryStream.Exists)
            {
                memoryStream.Obj.Dispose();
                memoryStream.RemoveObj();
            }
            writer.stream = null;
            if (objToItemInfo.Count > 0) objToItemInfo.Clear();
            itemInfoRecycler.ResetPooledItemInfos();            
        }

        CachedTypeWriter lastTypeHandler = null;
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

                Type itemType = GetItemTypeForSerialization(item);

                if (lastTypeHandlerType == itemType)
                {                    
                    lastTypeHandler.WriteItem(item, rootName);                    
                }
                else
                {
                    var typeHandler = GetCachedTypeWriter(itemType);
                    
                    typeHandler.WriteItem(item, rootName);                    

                    lastTypeHandler = typeHandler;
                    lastTypeHandlerType = typeHandler.HandlerType;
                }

                if (memoryStream.Obj.Position == 0)
                {
                    return Encoding.UTF8.GetString(writer.Buffer, 0, writer.BufferCount);
                }
                else
                {
                    writer.WriteBufferToStream();
                    return Encoding.UTF8.GetString(memoryStream.Obj.GetBuffer(), 0, (int)memoryStream.Obj.Position);
                }                                        
            }
            finally
            {
                FinishSerialization();
                serializerLock.Exit();
            }
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Type GetItemTypeForSerialization<T>(T item)
        {
            Type callType = typeof(T);
            if (!callType.IsValueType) return item.GetType();
            if (!callType.IsGenericType) return callType;

            Type itemType = item.GetType();
            if (callType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Type underlyingType = Nullable.GetUnderlyingType(callType);
                if (underlyingType == itemType) return callType;
            }

            return itemType;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateItemInfoForClass<T>(T item, ByteSegment itemName)
        {            
            currentItemInfo = itemInfoRecycler.TakeItemInfo(currentItemInfo, item, itemName);            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreateItemInfoForStruct(ByteSegment itemName)
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

                Type itemType = GetItemTypeForSerialization(item);

                if (lastTypeHandlerType == itemType)
                {                    
                    lastTypeHandler.WriteItem(item, rootName);                    
                }
                else
                {
                    var typeHandler = GetCachedTypeWriter(itemType);
                    
                    typeHandler.WriteItem(item, rootName);                    

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

        // Will only write async to the stream for the final data chunk,
        // so define a sufficient buffer, otherwise the intermediate writings will be blocking!
        public async Task SerializeAsync<T>(Stream stream, T item)
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

                Type itemType = GetItemTypeForSerialization(item);

                if (lastTypeHandlerType == itemType)
                {
                    lastTypeHandler.WriteItem(item, rootName);
                }
                else
                {
                    var typeHandler = GetCachedTypeWriter(itemType);

                    typeHandler.WriteItem(item, rootName);

                    lastTypeHandler = typeHandler;
                    lastTypeHandlerType = typeHandler.HandlerType;
                }

                await writer.WriteBufferToStreamAsync();
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
            else if (itemType == typeof(TimeSpan)) stringValueWriter.SetWriterMethod<TimeSpan>(value => writer.WritePrimitiveValueAsString(value.ToString()));

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
            else if (itemType == typeof(TimeSpan)) stringValueWriter.SetWriterMethod<TimeSpan>(value => writer.WriteStringValueAsStringWithCopy(value.ToString()));

            return stringValueWriter.HasMethod;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CachedTypeWriter GetCachedTypeWriter(Type itemType)
        {            
            return typeWriterCache.TryGetValue(itemType, out var cachedTypeHandler) ? cachedTypeHandler : CreateCachedTypeWriter(itemType);
        }


        private CachedTypeWriter CreateCachedTypeWriter(Type itemType)
        {
            CachedTypeWriter typeHandler = new CachedTypeWriter(this, itemType);            
            typeWriterCache[itemType] = typeHandler; // Typehandler must be added first for the case of recursion (type contains same type)

            typeHandler.preparedTypeInfo = writer.PrepareTypeInfo(itemType.GetSimplifiedTypeName());

            foreach(var creator in settings.itemHandlerCreators)
            {
                if (!creator.SupportsType(itemType)) continue;

                creator.CreateTypeHandler(extensionApi, typeHandler, itemType);
                return typeHandler;
            }

            bool isNullableValueType = itemType.IsValueType && itemType.IsNullable();
            if (isNullableValueType) itemType = Nullable.GetUnderlyingType(itemType);

            if (itemType == typeof(int)) CreateIntItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(uint)) CreateUIntItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(long)) CreateLongItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(ulong)) CreateULongItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(short)) CreateShortItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(ushort)) CreateUShortItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(sbyte)) CreateSByteItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(byte)) CreateByteItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(string)) CreateStringItemWriter(typeHandler);
            else if (itemType == typeof(float)) CreateFloatItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(double)) CreateDoubleItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(decimal)) CreateDecimalItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(char)) CreateCharItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(bool)) CreateBoolItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(IntPtr)) CreateIntPtrItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(UIntPtr)) CreateUIntPtrItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(Guid)) CreateGuidItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(DateTime)) CreateDateTimeItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(DateTimeOffset)) CreateDateTimeOffsetItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(TimeSpan)) CreateTimeSpanItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(JsonFragment)) CreateJsonFragmentItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(TextSegment)) CreateTextSegmentItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(Uri)) CreateUriItemWriter(typeHandler);
            else if (itemType.IsEnum) CreateAndSetItemHandlerViaReflection(itemType, nameof(CreateEnumItemHandler), typeHandler, isNullableValueType);            

            else if (settings.writeByteArrayAsBase64String && itemType == typeof(ByteSegment)) typeHandler.SetItemHandler_Primitive<ByteSegment>(writer.WriteByteSegmentValueAsBase64);
            else if (settings.writeByteArrayAsBase64String && itemType == typeof(byte[])) typeHandler.SetItemHandler_Primitive<byte[]>(writer.WriteByteArrayValueAsBase64);
            else if (settings.writeByteArrayAsBase64String && itemType == typeof(ArraySegment<byte>)) typeHandler.SetItemHandler_Primitive<ArraySegment<byte>>(writer.WriteByteArraySegmentValueAsBase64);

            else if (TryCreateDictionaryItemHandler(typeHandler, itemType)) {/* do nothing */}
            else if (TryCreateListItemHandler(typeHandler, itemType)) {/* do nothing */}
            else if (TryCreateEnumerableItemHandler(typeHandler, itemType)) {/* do nothing */}
            else CreateComplexItemHandler(typeHandler, itemType, isNullableValueType);           
            
            return typeHandler;

            void CreateAndSetItemHandlerViaReflection(Type itemType, string getItemHandlerMethodName, params object[] parameters)
            {
                MethodInfo method = typeof(JsonSerializer).GetMethod(getItemHandlerMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(itemType);                
                generic.Invoke(this, parameters);
            }
        }


        private void CreateEnumItemHandler<T>(CachedTypeWriter typeHandler, bool nullable) where T : struct, Enum
        {
            if (!nullable)
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
            else
            {
                if (settings.enumAsString)
                {
                    typeHandler.SetItemHandler_Primitive<Nullable<T>>(item =>
                    {
                        if (item.HasValue) writer.WriteStringValue(item.Value.ToName());
                        else writer.WriteNullValue();
                    });
                }
                else
                {
                    typeHandler.SetItemHandler_Primitive<Nullable<T>>(item =>
                    {
                        if (item.HasValue) writer.WriteIntValue(item.Value.ToInt());
                        else writer.WriteNullValue();
                    });
                }
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
        private bool TryHandleItemAsRef<T>(T item)
        {
            if (settings.referenceCheck == ReferenceCheck.NoRefCheck || currentItemInfo == null || item == null || !typeof(T).IsClass) return false;
            return TryHandleObjAsRef(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryHandleObjAsRef(object obj)
        {
            if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef)
            {
                if (!objToItemInfo.TryAdd(obj, currentItemInfo))
                {
                    writer.WriteRefObject(objToItemInfo[obj]);
                    return true;
                }
            }
            else
            {
                var itemInfo = currentItemInfo.parentInfo;
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
        private bool TypeInfoRequired(bool typeDeviating)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo) return true;
            if (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && typeDeviating) return true;
            return false;
        }

    }
}
