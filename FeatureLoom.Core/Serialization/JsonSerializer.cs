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
        readonly Dictionary<object, ItemInfo> objToItemInfo = new();
        readonly Dictionary<object, int> objToRefId = new();
        int nextRefId = 1;
        // Id assigned to the item currently being written, or 0 if the item is not id-tracked.
        int currentItemId;
        readonly ItemInfoRecycler itemInfoRecycler;
        private ByteSegment rootName;
        ItemInfo currentItemInfo;
        readonly ExtensionApi extensionApi;
        public delegate bool TryCreateItemHandlerDelegate<T>(ExtensionApi api, out Action<T> itemHandler, out JsonDataTypeCategory category);

        /// <summary>
        /// Creates a serializer and configures its settings with a callback, so an instance can be
        /// built in a single expression without preparing a <see cref="Settings"/> object first.
        /// </summary>
        /// <param name="buildSettings">Configuration action; may be <see langword="null"/>.</param>
        public JsonSerializer(Action<Settings> buildSettings) : this(Settings.Build(buildSettings))
        {

        }

        /// <summary>
        /// Creates a serializer from the given settings, or from the defaults if none are provided.
        /// The settings are compiled on construction, so later changes to the passed instance have
        /// no effect on this serializer.
        /// </summary>
        /// <param name="settings">Settings to use; may be <see langword="null"/>.</param>
        public JsonSerializer(Settings settings = null)
        {           
            this.settings = new CompiledSettings(settings ?? new Settings());
            writer = new JsonUTF8StreamWriter(this.settings);
            // Only the JSONPath format keeps ItemInfos alive beyond their scope, because the ref
            // values are built from the item name chain. The id based format does not need them.
            itemInfoRecycler = new ItemInfoRecycler(this.settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef && !this.settings.writeItemIds);
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
            if (objToRefId.Count > 0) objToRefId.Clear();
            nextRefId = 1;
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

        /// <summary>
        /// Builds the primitive wrapper (optional type info object) around a custom item handler.
        /// Used by the public extension API, where the item handler is a delegate and its
        /// indirection cannot be avoided. Internal primitive writers build the equivalent wrapper
        /// around a local function instead, saving one delegate invocation.
        /// </summary>
        internal Action<T, bool, ByteSegment> CreatePrimitiveItemWriter<T>(CachedTypeWriter typeHandler, Action<T> itemHandler)
        {
            if (typeHandler.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                return (item, _, _) =>
                {
                    StartTypeInfoObject(typeHandler.preparedTypeInfo);
                    itemHandler.Invoke(item);
                    FinishTypeInfoObject();
                };
            }
            else if (typeHandler.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
            {
                return (item, deviatingType, _) =>
                {
                    if (!deviatingType)
                    {
                        itemHandler.Invoke(item);
                    }
                    else
                    {
                        StartTypeInfoObject(typeHandler.preparedTypeInfo);
                        itemHandler.Invoke(item);
                        FinishTypeInfoObject();
                    }
                };
            }
            else
            {
                return (item, _, _) => itemHandler.Invoke(item);
            }
        }

        /// <summary>
        /// Builds the array wrapper (item info handling, reference check, type info) around a
        /// custom item handler, which is expected to write the array elements incl. separators.
        /// </summary>
        internal Action<T, bool, ByteSegment> CreateArrayItemWriter<T>(CachedTypeWriter typeHandler, Action<T> itemHandler)
        {
            if (settings.requiresItemInfos)
            {
                return (item, deviatingType, itemName) =>
                {
                    // TryHandleItemAsRef already returns false for value types, so no separate
                    // struct variant is needed; only the item info creation differs.
                    if (typeof(T).IsValueType) CreateItemInfoForStruct(itemName);
                    else CreateItemInfoForClass(item, itemName);
                    currentItemId = 0;
                    if (!TryHandleItemAsRef(item))
                    {
                        int id = currentItemId;
                        if (id != 0)
                        {
                            // The array itself must carry the "$id", so it is wrapped into an
                            // object with the actual elements moved into "$values".
                            writer.OpenObject();
                            writer.WriteItemId(id);
                            writer.WriteComma();
                            writer.WriteValuesFieldName();
                            WriteArrayWithTypeInfo(item, deviatingType, typeHandler, itemHandler);
                            writer.CloseObject();
                        }
                        else
                        {
                            WriteArrayWithTypeInfo(item, deviatingType, typeHandler, itemHandler);
                        }
                    }
                    UseParentItemInfo();
                };
            }
            else
            {
                return (item, deviatingType, _) => WriteArrayWithTypeInfo(item, deviatingType, typeHandler, itemHandler);
            }
        }

        /// <summary>
        /// Writes the array body, wrapped in a type info object if required.
        /// </summary>
        private void WriteArrayWithTypeInfo<T>(T item, bool deviatingType, CachedTypeWriter typeHandler, Action<T> itemHandler)
        {
            bool writeTypeInfo = TypeInfoRequired(typeHandler, deviatingType);
            if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);
            writer.OpenArray();
            itemHandler.Invoke(item);
            writer.CloseArray();
            if (writeTypeInfo) FinishTypeInfoObject();
        }

        /// <summary>
        /// Builds the object wrapper (item info handling, reference check, type info) around a
        /// custom item handler. Used by the public extension API, where the item handler is a
        /// delegate and its indirection cannot be avoided. Internal call sites build the
        /// equivalent wrapper around a local function instead, saving one delegate invocation.
        /// </summary>
        internal Action<T, bool, ByteSegment> CreateObjectItemWriter<T>(CachedTypeWriter typeHandler, Action<T> itemHandler)
        {
            if (settings.requiresItemInfos)
            {
                return (item, deviatingType, itemName) =>
                {
                    // TryHandleItemAsRef already returns false for value types, so no separate
                    // struct variant is needed; only the item info creation differs.
                    if (typeof(T).IsValueType) CreateItemInfoForStruct(itemName);
                    else CreateItemInfoForClass(item, itemName);
                    currentItemId = 0;
                    if (!TryHandleItemAsRef(item))
                    {
                        int id = currentItemId;
                        writer.OpenObject();
                        WriteTypeInfoAndBody(item, deviatingType, typeHandler, itemHandler, id);
                        writer.CloseObject();
                    }
                    UseParentItemInfo();
                };
            }
            else
            {
                return (item, deviatingType, _) =>
                {
                    writer.OpenObject();
                    WriteTypeInfoAndBody(item, deviatingType, typeHandler, itemHandler);
                    writer.CloseObject();
                };
            }
        }

        /// <summary>
        /// Writes the optional type info followed by the object body. If the body turns out to be
        /// empty, the separating comma written after the type info is rolled back again.
        /// </summary>
        private void WriteTypeInfoAndBody<T>(T item, bool deviatingType, CachedTypeWriter typeHandler, Action<T> itemHandler, int itemId = 0)
        {
            int countBeforeComma = -1;
            int countAfterComma = -1;
            if (itemId != 0)
            {
                // "$id" has to be the first member so that other serializers recognize it.
                writer.WriteItemId(itemId);
                countBeforeComma = writer.BufferCount;
                writer.WriteComma();
                countAfterComma = writer.BufferCount;
            }
            if (TypeInfoRequired(typeHandler, deviatingType))
            {
                writer.WriteToBuffer(typeHandler.preparedTypeInfo);
                countBeforeComma = writer.BufferCount;
                writer.WriteComma();
                countAfterComma = writer.BufferCount;
            }
            itemHandler.Invoke(item);
            if (writer.BufferCount == countAfterComma) writer.BufferCount = countBeforeComma;
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

        /// <summary>
        /// Creates the writer that writes a dictionary key of the given type as a JSON string.
        /// This is only called while a dictionary type handler is created, so it is not on the
        /// write path and needs no caching.
        /// Returns false if the type is not supported as a key, in which case the dictionary is
        /// not written as a JSON object.
        /// </summary>
        private bool TryCreateKeyWriter(Type keyType, out CachedKeyWriter keyWriter)
        {
            keyWriter = new CachedKeyWriter();

            // Enums are written via their underlying representation, which the generic helper
            // resolves, so they are handled before the concrete type checks.
            if (keyType.IsEnum)
            {
                CreateAndSetKeyWriterViaReflection(keyType, keyWriter);
                return keyWriter.HasMethod;
            }

            if (keyType == typeof(string))
            {
                keyWriter.SetWriterMethod<string>(writer.WritePrimitiveValueAsString);
                keyWriter.SetWriterWithCopyMethod<string>(writer.WriteStringValueAsStringWithCopy);
            }
            else if (keyType == typeof(bool))
            {
                keyWriter.SetWriterMethod<bool>(writer.WriteBoolAsStringValue);
                keyWriter.SetWriterWithCopyMethod<bool>(writer.WriteBoolValueAsStringWithCopy);
            }
            else if (keyType == typeof(char))
            {
                keyWriter.SetWriterMethod<char>(writer.WriteCharValueAsString);
                keyWriter.SetWriterWithCopyMethod<char>(writer.WriteCharValueAsStringWithCopy);
            }
            else if (keyType == typeof(sbyte))
            {
                keyWriter.SetWriterMethod<sbyte>(writer.WriteSbyteValueAsString);
                keyWriter.SetWriterWithCopyMethod<sbyte>(writer.WriteSbyteValueAsStringWithCopy);
            }
            else if (keyType == typeof(short))
            {
                keyWriter.SetWriterMethod<short>(writer.WriteShortValueAsString);
                keyWriter.SetWriterWithCopyMethod<short>(writer.WriteShortValueAsStringWithCopy);
            }
            else if (keyType == typeof(int))
            {
                keyWriter.SetWriterMethod<int>(writer.WriteIntValueAsString);
                keyWriter.SetWriterWithCopyMethod<int>(writer.WriteIntValueAsStringWithCopy);
            }
            else if (keyType == typeof(long))
            {
                keyWriter.SetWriterMethod<long>(writer.WriteLongValueAsString);
                keyWriter.SetWriterWithCopyMethod<long>(writer.WriteLongValueAsStringWithCopy);
            }
            else if (keyType == typeof(byte))
            {
                keyWriter.SetWriterMethod<byte>(writer.WriteByteAsStringValue);
                keyWriter.SetWriterWithCopyMethod<byte>(writer.WriteByteValueAsStringWithCopy);
            }
            else if (keyType == typeof(ushort))
            {
                keyWriter.SetWriterMethod<ushort>(writer.WriteUshortValueAsString);
                keyWriter.SetWriterWithCopyMethod<ushort>(writer.WriteUshortValueAsStringWithCopy);
            }
            else if (keyType == typeof(uint))
            {
                keyWriter.SetWriterMethod<uint>(writer.WriteUintValueAsString);
                keyWriter.SetWriterWithCopyMethod<uint>(writer.WriteUintValueAsStringWithCopy);
            }
            else if (keyType == typeof(ulong))
            {
                keyWriter.SetWriterMethod<ulong>(writer.WriteUlongValueAsString);
                keyWriter.SetWriterWithCopyMethod<ulong>(writer.WriteUlongValueAsStringWithCopy);
            }
            else if (keyType == typeof(float))
            {
                keyWriter.SetWriterMethod<float>(writer.WriteFloatValueAsString);
                keyWriter.SetWriterWithCopyMethod<float>(writer.WriteFloatValueAsStringWithCopy);
            }
            else if (keyType == typeof(double))
            {
                keyWriter.SetWriterMethod<double>(writer.WriteDoubleValueAsString);
                keyWriter.SetWriterWithCopyMethod<double>(writer.WriteDoubleValueAsStringWithCopy);
            }
            else if (keyType == typeof(decimal))
            {
                keyWriter.SetWriterMethod<decimal>(writer.WriteDecimalValueAsString);
                keyWriter.SetWriterWithCopyMethod<decimal>(writer.WriteDecimalValueAsStringWithCopy);
            }
            else if (keyType == typeof(Guid))
            {
                keyWriter.SetWriterMethod<Guid>(writer.WriteGuidValue);
                keyWriter.SetWriterWithCopyMethod<Guid>(writer.WriteGuidValueWithCopy);
            }
            else if (keyType == typeof(DateTime))
            {
                keyWriter.SetWriterMethod<DateTime>(writer.WriteDateTimeValue);
                keyWriter.SetWriterWithCopyMethod<DateTime>(writer.WriteDateTimeValueWithCopy);
            }
            else if (keyType == typeof(DateTimeOffset))
            {
                keyWriter.SetWriterMethod<DateTimeOffset>(writer.WriteDateTimeOffsetValue);
                keyWriter.SetWriterWithCopyMethod<DateTimeOffset>(writer.WriteDateTimeOffsetValueWithCopy);
            }
            else if (keyType == typeof(TimeSpan))
            {
                keyWriter.SetWriterMethod<TimeSpan>(writer.WriteTimeSpanValue);
                keyWriter.SetWriterWithCopyMethod<TimeSpan>(writer.WriteTimeSpanValueWithCopy);
            }
#if NET6_0_OR_GREATER
            else if (keyType == typeof(DateOnly))
            {
                keyWriter.SetWriterMethod<DateOnly>(writer.WriteDateOnlyValue);
                keyWriter.SetWriterWithCopyMethod<DateOnly>(writer.WriteDateOnlyValueWithCopy);
            }
            else if (keyType == typeof(TimeOnly))
            {
                keyWriter.SetWriterMethod<TimeOnly>(writer.WriteTimeOnlyValue);
                keyWriter.SetWriterWithCopyMethod<TimeOnly>(writer.WriteTimeOnlyValueWithCopy);
            }
#endif

            return keyWriter.HasMethod;
        }

        /// <summary>
        /// Creates the key writer for an enum type, which requires the enum type as a generic
        /// type parameter and can therefore only be reached via reflection.
        /// </summary>
        private void CreateAndSetKeyWriterViaReflection(Type keyType, CachedKeyWriter keyWriter)
        {
            MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateEnumKeyWriter), BindingFlags.NonPublic | BindingFlags.Instance);
            createMethod.MakeGenericMethod(keyType).Invoke(this, new object[] { keyWriter });
        }

        /// <summary>
        /// Enum keys are written by their name, matching how enums are written as values when
        /// enumAsString is set. Otherwise the numeric value is used.
        /// </summary>
        private void CreateEnumKeyWriter<T>(CachedKeyWriter keyWriter) where T : struct, Enum
        {
            if (settings.enumAsString)
            {
                keyWriter.SetWriterMethod<T>(value => writer.WritePrimitiveValueAsString(value.ToString()));
                keyWriter.SetWriterWithCopyMethod<T>(value => writer.WriteStringValueAsStringWithCopy(value.ToString()));
            }
            else
            {
                keyWriter.SetWriterMethod<T>(value => writer.WriteLongValueAsString(Convert.ToInt64(value)));
                keyWriter.SetWriterWithCopyMethod<T>(value => writer.WriteLongValueAsStringWithCopy(Convert.ToInt64(value)));
            }
        }



        /// <summary>
        /// Determines the name written into the "$type" member of the given type.
        /// Precedence: a per-type name set via ConfigureType wins, then a globally registered
        /// custom name, then the format setting, where generic types use genericTypeNameFormat and
        /// all other types use typeNameFormat.
        /// Only called while creating a CachedTypeWriter, so the result is cached per type and
        /// does not add any cost to the actual serialization.
        /// </summary>
        private string ResolveTypeName(Type itemType)
        {
            if (settings.TryGetCustomTypeName(itemType, out string perTypeName)) return perTypeName;

            if (settings.customTypeNames != null &&
                settings.customTypeNames.TryGetValue(itemType, out string customName)) return customName;

            var format = itemType.IsGenericType ? settings.genericTypeNameFormat : settings.typeNameFormat;

            switch (format)
            {
                case TypeNameFormat.FullName: return TypeNameHelper.Shared.GetFullTypeName(itemType);
                case TypeNameFormat.AssemblyQualified: return TypeNameHelper.Shared.GetAssemblyQualifiedTypeName(itemType);
                default: return itemType.GetSimplifiedTypeName();
            }
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

            typeHandler.preparedTypeInfo = writer.PrepareTypeInfo(ResolveTypeName(itemType));

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
#if NET6_0_OR_GREATER
            else if (itemType == typeof(DateOnly)) CreateDateOnlyItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(TimeOnly)) CreateTimeOnlyItemWriter(typeHandler, isNullableValueType);
#endif
            else if (itemType == typeof(JsonFragment)) CreateJsonFragmentItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(TextSegment)) CreateTextSegmentItemWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(Uri)) CreateUriItemWriter(typeHandler);
            else if (itemType.IsEnum) CreateAndSetItemHandlerViaReflection(itemType, nameof(CreateEnumItemHandler), typeHandler, isNullableValueType);
            else if (itemType == typeof(ByteSegment)) CreateByteSegmentWriter(typeHandler, isNullableValueType);
            else if (itemType == typeof(byte[])) CreateByteArrayWriter(typeHandler);
            else if (itemType == typeof(ArraySegment<byte>)) CreateByteArraySegmentWriter(typeHandler, isNullableValueType);

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
            if (settings.writeItemIds)
            {
                // Id based format: the first occurrence gets a fresh id, which the object/array
                // writer emits as "$id", every repeated occurrence becomes {"$ref":"<id>"}.
                if (objToRefId.TryGetValue(obj, out int existingId))
                {
                    if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef || IsAncestor(obj))
                    {
                        writer.WriteRefObjectById(existingId);
                        return true;
                    }
                    currentItemId = existingId;
                    return false;
                }

                currentItemId = nextRefId++;
                objToRefId[obj] = currentItemId;
                return false;
            }

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

        /// <summary>
        /// Checks whether the given object is currently being written further up the stack, which
        /// means writing it again would create an endless loop.
        /// </summary>
        private bool IsAncestor(object obj)
        {
            var itemInfo = currentItemInfo?.parentInfo;
            while (itemInfo != null)
            {
                if (itemInfo.objItem == obj) return true;
                itemInfo = itemInfo.parentInfo;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TypeInfoRequired(CachedTypeWriter typeHandler, bool typeDeviating)
        {
            if (typeHandler.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo) return true;
            if (typeHandler.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && typeDeviating) return true;
            return false;
        }

    }
}
