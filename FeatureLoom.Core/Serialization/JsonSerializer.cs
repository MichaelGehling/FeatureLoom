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
                    StartTypeInfoObject(typeHandler);
                    itemHandler.Invoke(item);
                    FinishTypeInfoObject(typeHandler);
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
                        StartTypeInfoObject(typeHandler);
                        itemHandler.Invoke(item);
                        FinishTypeInfoObject(typeHandler);
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
            if (writeTypeInfo) StartTypeInfoObject(typeHandler, true);
            writer.OpenArray();
            itemHandler.Invoke(item);
            writer.CloseArray();
            if (writeTypeInfo) FinishTypeInfoObject(typeHandler);
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
                if (typeHandler.typeInfoFormat == TypeInfoFormat.AlwaysEnvelope)
                {
                    // The body is moved into a nested "$value" object, so that type info and
                    // payload always sit at fixed positions, no matter the value's shape.
                    writer.WriteComma();
                    writer.WriteValueFieldName();
                    writer.OpenObject();
                    itemHandler.Invoke(item);
                    writer.CloseObject();
                    return;
                }
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
        /// <param name="typeSettings">
        /// Settings of the dictionary type, used to find a configured key formatter. A formatter
        /// takes precedence over the built-in key handling and makes any key type writable.
        /// </param>
        private bool TryCreateKeyWriter(Type keyType, BaseTypeWriteSettings typeSettings, out CachedKeyWriter keyWriter)
        {
            keyWriter = new CachedKeyWriter();

            var keyFormatter = typeSettings?.keyFormatter;
            if (keyFormatter != null && keyFormatter.KeyType == keyType)
            {
                keyFormatter.BindTo(writer, keyWriter);
                return keyWriter.HasMethod;
            }

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
        /// True if <paramref name="keyType"/> can be written as a JSON property name, i.e. a
        /// dictionary with such a key is written as a JSON object rather than as an array of
        /// key/value pairs.
        /// </summary>
        /// <remarks>
        /// Type-only counterpart of <see cref="TryCreateKeyWriter"/>, which needs an instance
        /// because it binds the actual writer methods. Both must accept the same set of types.
        /// </remarks>
        internal static bool CanWriteKeyAsPropertyName(Type keyType)
        {
            if (keyType == null) return false;
            if (keyType.IsEnum) return true;

            return keyType == typeof(string) ||
                   keyType == typeof(bool) ||
                   keyType == typeof(char) ||
                   keyType == typeof(sbyte) ||
                   keyType == typeof(short) ||
                   keyType == typeof(int) ||
                   keyType == typeof(long) ||
                   keyType == typeof(byte) ||
                   keyType == typeof(ushort) ||
                   keyType == typeof(uint) ||
                   keyType == typeof(ulong) ||
                   keyType == typeof(float) ||
                   keyType == typeof(double) ||
                   keyType == typeof(decimal) ||
                   keyType == typeof(Guid) ||
                   keyType == typeof(DateTime) ||
                   keyType == typeof(DateTimeOffset) ||
                   keyType == typeof(TimeSpan)
#if NET6_0_OR_GREATER
                   || keyType == typeof(DateOnly)
                   || keyType == typeof(TimeOnly)
#endif
                   ;
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
        /// Precedence: a custom name set via SetCustomTypeName wins, then the format setting,
        /// where generic types use genericTypeNameFormat and all other types use typeNameFormat.
        /// Only called while creating a CachedTypeWriter, so the result is cached per type and
        /// does not add any cost to the actual serialization.
        /// </summary>
        internal string ResolveTypeName(Type itemType)
        {
            if (settings.TryGetCustomTypeName(itemType, out string customName)) return customName;

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

        /// <summary>
        /// Builds a resolver that returns the writer to use for a value whose runtime type may
        /// deviate from the declared one. If <paramref name="contextSettings"/> carries overrides
        /// that are transferable to another type, the deviating writer is built with them, so a
        /// member/context override stays in effect for polymorphic values.
        /// <para>
        /// Such context-local writers cannot go into the shared per-type cache, so they are cached
        /// in a dictionary owned by this resolver, i.e. per prepared call site. Without transferable
        /// overrides the shared cache is used and no extra dictionary is allocated.
        /// </para>
        /// </summary>
        /// <param name="contextSettings">Settings the declared writer was built with, may be null.</param>
        internal Func<Type, CachedTypeWriter> CreateDeviatingWriterResolver(BaseTypeWriteSettings contextSettings)
        {
            var transferable = contextSettings?.GetTransferableSubset();
            if (transferable == null) return GetCachedTypeWriter;

            var localCache = new Dictionary<Type, CachedTypeWriter>();
            return valueType =>
            {
                if (localCache.TryGetValue(valueType, out var cached)) return cached;
                // Bypasses the shared cache on purpose: the result is only valid in this context.
                var created = CreateCachedTypeWriter(valueType, transferable);
                localCache[valueType] = created;
                return created;
            };
        }

        /// <summary>
        /// Builds a delegate that writes a value of the declared type <typeparamref name="TValue"/>
        /// but respects polymorphy: if the runtime type deviates from the declared one, the writer
        /// of the runtime type is used, so a value declared as e.g. <see cref="object"/> is written
        /// with its actual members instead of an empty object.
        /// <para>
        /// If the declared type cannot deviate at runtime (value type or sealed class), the check
        /// is skipped entirely and the prepared writer is called directly.
        /// </para>
        /// </summary>
        /// <param name="declaredWriter">Writer prepared for <typeparamref name="TValue"/>.</param>
        /// <param name="contextSettings">
        /// Settings <paramref name="declaredWriter"/> was built with, so their transferable part can
        /// be applied to deviating runtime types as well. May be <see langword="null"/>.
        /// </param>
        internal Action<TValue, ByteSegment> CreatePolymorphicValueWriter<TValue>(CachedTypeWriter declaredWriter, BaseTypeWriteSettings contextSettings = null)
        {
            Type declaredType = typeof(TValue);
            if (declaredType.IsValueType || declaredType.IsSealed)
            {
                return (value, itemName) => declaredWriter.WriteItem(value, itemName);
            }

            Type declaredHandlerType = declaredWriter.HandlerType;
            var resolveDeviating = CreateDeviatingWriterResolver(contextSettings);
            return (value, itemName) =>
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }
                Type valueType = value.GetType();
                CachedTypeWriter actualWriter = valueType == declaredHandlerType ? declaredWriter : resolveDeviating(valueType);
                actualWriter.WriteItem(value, itemName);
            };
        }

        /// <summary>
        /// True if values written through <paramref name="declaredWriter"/> can never contain a
        /// reference path, taking runtime polymorphy into account.
        /// </summary>
        /// <remarks>
        /// The writer's own <see cref="CachedTypeWriter.NoRefTypes"/> only describes the declared
        /// type. That is not sufficient for a type that can deviate at runtime: a custom value
        /// shape writer may declare no references for a non sealed reference type, while a derived
        /// runtime type is written by a different writer whose children can contain references.
        /// </remarks>
        internal static bool NoRefTypesIncludingRuntimeTypes<TValue>(CachedTypeWriter declaredWriter)
            => NoRefTypesIncludingRuntimeTypes(declaredWriter, typeof(TValue));

        /// <inheritdoc cref="NoRefTypesIncludingRuntimeTypes{TValue}(CachedTypeWriter)"/>
        /// <param name="declaredWriter">Writer prepared for <paramref name="declaredType"/>.</param>
        /// <param name="declaredType">Statically known type of the written values.</param>
        internal static bool NoRefTypesIncludingRuntimeTypes(CachedTypeWriter declaredWriter, Type declaredType)
        {
            if (!declaredWriter.NoRefTypes) return false;
            return declaredType.IsValueType || declaredType.IsSealed;
        }

        /// <summary>
        /// Returns the writer for <paramref name="itemType"/>, optionally built with locally
        /// overriding settings instead of the settings resolved from the type itself.
        /// <para>
        /// With <paramref name="typeSettings"/> set, the shared per-type cache is bypassed and a
        /// fresh writer is built, because the result is only valid in the context the override came
        /// from (e.g. one member). Mirrors <c>JsonDeserializer.GetCachedTypeReader(Type, BaseTypeSettings)</c>.
        /// </para>
        /// <para>
        /// This terminates even for recursive types: overrides form a finite tree (every
        /// <c>ConfigureMember</c> creates a fresh settings object, so no settings object can contain
        /// itself), and each nesting level consumes one configured level. The innermost level has no
        /// member settings left and therefore falls back to the cached, recursion-safe writer.
        /// </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CachedTypeWriter GetCachedTypeWriter(Type itemType, BaseTypeWriteSettings typeSettings)
        {
            if (typeSettings == null) return GetCachedTypeWriter(itemType);
            return CreateCachedTypeWriter(itemType, typeSettings);
        }

        /// <summary>
        /// Returns the writer to use for a member of type <paramref name="fieldType"/>.
        /// <para>
        /// Only builds a member-local writer if <paramref name="memberSettings"/> actually changes
        /// how the value is written. Settings that are pure member metadata (ignore, alternate
        /// name) are handled by the field writer itself and must keep using the shared writer,
        /// otherwise every renamed member would get its own duplicate writer for no reason.
        /// </para>
        /// </summary>
        private CachedTypeWriter GetCachedTypeWriterForMember(Type fieldType, BaseTypeWriteSettings memberSettings)
        {
            if (memberSettings == null || !memberSettings.HasValueShapingOverrides) return GetCachedTypeWriter(fieldType);
            return CreateCachedTypeWriter(fieldType, memberSettings);
        }

        /// <summary>
        /// Returns the writer to use for the elements of a container, applying the element settings
        /// configured for the container via ConfigureElement, if any.
        /// </summary>
        /// <remarks>
        /// The configured element type is verified against <paramref name="elementType"/>, because
        /// for a generic type definition it can only be checked once the type is constructed. On a
        /// mismatch the settings are ignored and the shared writer is used.
        /// </remarks>
        private CachedTypeWriter GetCachedTypeWriterForElement(Type elementType, BaseTypeWriteSettings containerSettings)
        {
            var elementSettings = GetElementSettings(elementType, containerSettings);
            if (elementSettings == null) return GetCachedTypeWriter(elementType);
            return CreateCachedTypeWriter(elementType, elementSettings);
        }

        /// <summary>
        /// Returns the element settings configured on <paramref name="containerSettings"/> if they
        /// apply to <paramref name="elementType"/> and actually change how a value is written,
        /// otherwise <see langword="null"/>.
        /// </summary>
        private static BaseTypeWriteSettings GetElementSettings(Type elementType, BaseTypeWriteSettings containerSettings)
        {
            var elementSettings = containerSettings?.elementSettings;
            if (elementSettings == null) return null;
            if (containerSettings.elementSettingsType != elementType) return null;
            if (!elementSettings.HasValueShapingOverrides) return null;
            return elementSettings;
        }

        /// <summary>
        /// Builds the resolver used by container handlers for elements whose runtime type deviates
        /// from the declared element type, so configured element settings keep applying to them.
        /// </summary>
        private Func<Type, CachedTypeWriter> CreateDeviatingElementWriterResolver(Type elementType, BaseTypeWriteSettings containerSettings)
            => CreateDeviatingWriterResolver(GetElementSettings(elementType, containerSettings));

        private CachedTypeWriter CreateCachedTypeWriter(Type itemType, BaseTypeWriteSettings typeSettings = null)
        {
            bool isLocalOverride = typeSettings != null;
            if (!isLocalOverride) settings.TryGetTypeSettings(itemType, out typeSettings);
            // A local override only states what it wants to change, so everything else must still
            // come from the settings configured for the type itself.
            else if (settings.TryGetTypeSettings(itemType, out var generalSettings)) typeSettings = typeSettings.MergeOnto(generalSettings);

            CachedTypeWriter typeHandler = new CachedTypeWriter(this, itemType, typeSettings);
            // Typehandler must be added first for the case of recursion (type contains same type).
            // Override variants are intentionally not cached: they are context-local and their
            // nesting is bounded by the configuration depth (see GetCachedTypeWriter).
            if (!isLocalOverride) typeWriterCache[itemType] = typeHandler;

            // A name set on a local override (e.g. member settings) applies only in that context,
            // so it is taken directly instead of through the per-type lookup in ResolveTypeName.
            string typeName = isLocalOverride && typeSettings.customTypeName != null
                ? typeSettings.customTypeName
                : ResolveTypeName(itemType);
            typeHandler.preparedTypeInfo = writer.PrepareTypeInfo(typeName);

            // A custom writer set for the type itself is found by direct lookup and therefore
            // always wins over the predicate registered, convention based handlers.
            ITypeHandlerCreator matchingCreator = typeSettings?.customTypeWriterCreator;
            if (matchingCreator == null)
            {
                foreach (var creator in settings.itemHandlerCreators)
                {
                    if (!creator.SupportsType(itemType)) continue;
                    matchingCreator = creator;
                    break;
                }
            }
            if (matchingCreator != null)
            {
                matchingCreator.CreateTypeHandler(this, typeHandler, itemType);
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
        void StartTypeInfoObject(CachedTypeWriter typeWriter, bool isArray = false)
        {
            // In OnlyInlineForObjects mode the envelope is dropped entirely, so the value is
            // written plainly and simply carries no type info.
            if (typeWriter.skipTypeInfoEnvelope) return;
            writer.OpenObject();
            writer.WriteToBuffer(typeWriter.preparedTypeInfo);
            writer.WriteComma();
            if (isArray && typeWriter.useValuesFieldName) writer.WriteValuesFieldName();
            else writer.WriteValueFieldName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FinishTypeInfoObject(CachedTypeWriter typeWriter)
        {
            if (typeWriter.skipTypeInfoEnvelope) return;
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
