using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;
using static FeatureLoom.Serialization.JsonDeserializer;
using static FeatureLoom.Serialization.JsonSerializer;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    Dictionary<Type, CachedTypeReader> typeReaderCache = new();
    Dictionary<ByteSegment, CachedTypeReader> proposedTypeReaderCache = new();
    readonly Dictionary<Type, bool> forbiddenTypeCache = new();

    CachedTypeReader CreateCachedTypeReader(Type itemType, BaseTypeSettings typeSettings = null)
    {
        bool overriddenTypeSettings = typeSettings != null;
        if (!overriddenTypeSettings)
        {
            settings.typeSettingsDict.TryGetValue(itemType, out typeSettings);
        }        

        if (IsForbiddenType(itemType))
        {
            throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(itemType)} is forbidden for deserialization.");
        }
        if (settings.typeWhitelistMode == Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes && !IsWhitelistedType(itemType))
        {
            throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(itemType)} is not whitelisted for deserialization.");
        }

        // If a base type is mapped to a more specific type, we create a reader for the mapped type and cache it for the original type,
        // so we can reuse it if the original type is encountered again.
        // This is usually used for abstract types that are mapped to a concrete implementation,
        // but it can also be used to map a concrete type to a derived type if the derived type.
        if (settings.typeMapping.TryGetValue(itemType, out Type mappedType))
        {
            CachedTypeReader mappedTypeReader = CreateCachedTypeReader(mappedType, overriddenTypeSettings ? typeSettings : null);
            if (!overriddenTypeSettings) typeReaderCache[itemType] = mappedTypeReader;
            return mappedTypeReader;
        }

        // For generic types we also check if there is a mapping for the generic type definition,
        // so we can map e.g. all IReadOnlyList<T> to List<T>.
        if (itemType.IsGenericType && settings.genericTypeMapping.Count > 0)
        {
            Type genericType = itemType.GetGenericTypeDefinition();
            if (settings.genericTypeMapping.TryGetValue(genericType, out Type genericMappedType))
            {
                mappedType = genericMappedType.MakeGenericType(itemType.GenericTypeArguments);
                CachedTypeReader mappedTypeReader = CreateCachedTypeReader(mappedType, overriddenTypeSettings ? typeSettings : null);
                if (!overriddenTypeSettings) typeReaderCache[itemType] = mappedTypeReader;
                return mappedTypeReader;
            }
        }

        return new CachedTypeReader((cachedTypeReader) =>
        {
            if (!overriddenTypeSettings) typeReaderCache[itemType] = cachedTypeReader;

            if (settings.customTypeReaders.TryGetValue(itemType, out object customReaderObj))
            {
                return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateCustomTypeReader), itemType.ToSingleEntryArray(), customReaderObj, typeSettings);
            }

            if (itemType.IsArray) return CreateArrayTypeReader(itemType, cachedTypeReader, typeSettings);
            else if (itemType == typeof(string)) return TypeReaderInitializer.Create(this, ReadStringValueOrNull, null, false, typeSettings);
            else if (itemType == typeof(long)) return TypeReaderInitializer.Create(this, ReadLongValue, null, false, typeSettings);
            else if (itemType == typeof(long?)) return TypeReaderInitializer.Create(this, ReadNullableLongValue, null, false, typeSettings);
            else if (itemType == typeof(int)) return TypeReaderInitializer.Create(this, ReadIntValue, null, false, typeSettings);
            else if (itemType == typeof(int?)) return TypeReaderInitializer.Create(this, ReadNullableIntValue, null, false, typeSettings);
            else if (itemType == typeof(short)) return TypeReaderInitializer.Create(this, ReadShortValue, null, false, typeSettings);
            else if (itemType == typeof(short?)) return TypeReaderInitializer.Create(this, ReadNullableShortValue, null, false, typeSettings);
            else if (itemType == typeof(sbyte)) return TypeReaderInitializer.Create(this, ReadSbyteValue, null, false, typeSettings);
            else if (itemType == typeof(sbyte?)) return TypeReaderInitializer.Create(this, ReadNullableSbyteValue, null, false, typeSettings);
            else if (itemType == typeof(ulong)) return TypeReaderInitializer.Create(this, ReadUlongValue, null, false, typeSettings);
            else if (itemType == typeof(ulong?)) return TypeReaderInitializer.Create(this, ReadNullableUlongValue, null, false, typeSettings);
            else if (itemType == typeof(uint)) return TypeReaderInitializer.Create(this, ReadUintValue, null, false, typeSettings);
            else if (itemType == typeof(uint?)) return TypeReaderInitializer.Create(this, ReadNullableUintValue, null, false, typeSettings);
            else if (itemType == typeof(ushort)) return TypeReaderInitializer.Create(this, ReadUshortValue, null, false, typeSettings);
            else if (itemType == typeof(ushort?)) return TypeReaderInitializer.Create(this, ReadNullableUshortValue, null, false, typeSettings);
            else if (itemType == typeof(byte)) return TypeReaderInitializer.Create(this, ReadByteValue, null, false, typeSettings);
            else if (itemType == typeof(byte?)) return TypeReaderInitializer.Create(this, ReadNullableByteValue, null, false, typeSettings);
            else if (itemType == typeof(double)) return TypeReaderInitializer.Create(this, ReadDoubleValue, null, false, typeSettings);
            else if (itemType == typeof(double?)) return TypeReaderInitializer.Create(this, ReadNullableDoubleValue, null, false, typeSettings);
            else if (itemType == typeof(float)) return TypeReaderInitializer.Create(this, ReadFloatValue, null, false, typeSettings);
            else if (itemType == typeof(float?)) return TypeReaderInitializer.Create(this, ReadNullableFloatValue, null, false, typeSettings);
            else if (itemType == typeof(decimal)) return TypeReaderInitializer.Create(this, ReadDecimalValue, null, false, typeSettings);
            else if (itemType == typeof(decimal?)) return TypeReaderInitializer.Create(this, ReadNullableDecimalValue, null, false, typeSettings);
            else if (itemType == typeof(bool)) return TypeReaderInitializer.Create(this, ReadBoolValue, null, false, typeSettings);
            else if (itemType == typeof(bool?)) return TypeReaderInitializer.Create(this, ReadNullableBoolValue, null, false, typeSettings);
            else if (itemType == typeof(char)) return TypeReaderInitializer.Create(this, ReadCharValue, null, false, typeSettings);
            else if (itemType == typeof(char?)) return TypeReaderInitializer.Create(this, ReadNullableCharValue, null, false, typeSettings);
            else if (itemType == typeof(DateTime)) return TypeReaderInitializer.Create(this, ReadDateTimeValue, null, false, typeSettings);
            else if (itemType == typeof(DateTime?)) return TypeReaderInitializer.Create(this, ReadNullableDateTimeValue, null, false, typeSettings);
            else if (itemType == typeof(DateTimeOffset)) return TypeReaderInitializer.Create(this, ReadDateTimeOffsetValue, null, false, typeSettings);
            else if (itemType == typeof(DateTimeOffset?)) return TypeReaderInitializer.Create(this, ReadNullableDateTimeOffsetValue, null, false, typeSettings);
            else if (itemType == typeof(TimeSpan)) return TypeReaderInitializer.Create(this, ReadTimeSpanValue, null, false, typeSettings);
            else if (itemType == typeof(TimeSpan?)) return TypeReaderInitializer.Create(this, ReadNullableTimeSpanValue, null, false, typeSettings);
            else if (itemType == typeof(Guid)) return TypeReaderInitializer.Create(this, ReadGuidValue, null, false, typeSettings);
            else if (itemType == typeof(Guid?)) return TypeReaderInitializer.Create(this, ReadNullableGuidValue, null, false, typeSettings);
            else if (itemType == typeof(JsonFragment)) return TypeReaderInitializer.Create(this, ReadJsonFragmentValue, null, false, typeSettings);
            else if (itemType == typeof(JsonFragment?)) return TypeReaderInitializer.Create(this, ReadNullableJsonFragmentValue, null, false, typeSettings);
            else if (itemType == typeof(IntPtr)) return TypeReaderInitializer.Create(this, ReadIntPtrValue, null, false, typeSettings);
            else if (itemType == typeof(UIntPtr)) return TypeReaderInitializer.Create(this, ReadUIntPtrValue, null, false, typeSettings);
            else if (itemType == typeof(Uri)) return TypeReaderInitializer.Create(this, () => { var s = ReadStringValueOrNull(); return s == null ? null : new Uri(s); }, null, false, typeSettings);
            else if (itemType == typeof(ByteSegment)) return CreateByteSegmentTypeReader(typeSettings);
            else if (itemType == typeof(ByteSegment?)) return CreateNullableByteSegmentTypeReader(typeSettings);
            else if (itemType == typeof(ArraySegment<byte>)) return CreateByteArraySegmentTypeReader(typeSettings);
            else if (itemType == typeof(ArraySegment<byte>?)) return CreateNullableByteArraySegmentTypeReader(typeSettings);
            else if (itemType == typeof(TextSegment)) return CreateTextSegmentTypeReader(typeSettings);
            else if (itemType.IsEnum || (Nullable.GetUnderlyingType(itemType)?.IsEnum ?? false))
            {
                if (!itemType.IsNullable()) return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateEnumReader), [ itemType ], typeSettings);
                else return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateNullableEnumReader), [ Nullable.GetUnderlyingType(itemType) ], typeSettings);
            }
            else if (itemType == typeof(object)) return CreateUnknownObjectReader(cachedTypeReader, typeSettings);
            else if (TryCreateDictionaryTypeReader(itemType, typeSettings, out TypeReaderInitializer initializer)) return initializer;
            else if (TryCreateEnumerableTypeReader(itemType, typeSettings, out initializer)) return initializer;
            else return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { itemType }, true, typeSettings);
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsForbiddenType(Type itemType)
    {
        if (forbiddenTypeCache.TryGetValue(itemType, out bool cached)) return cached;

        bool forbidden = false;

        // 1) direct match
        if (settings.forbiddenTypes.Contains(itemType))
        {
            forbidden = true;
        }
        // 2) generic definition match (e.g. Task<int> vs Task<>)
        else if (itemType.IsGenericType && settings.forbiddenTypes.Contains(itemType.GetGenericTypeDefinition()))
        {
            forbidden = true;
        }
        // 3) delegate family (covers Func<...>, Action<...>, custom delegates)
        else if (settings.forbiddenTypes.Contains(typeof(Delegate)) && typeof(Delegate).IsAssignableFrom(itemType))
        {
            forbidden = true;
        }
        // 4) expression family
        else if (settings.forbiddenTypes.Contains(typeof(System.Linq.Expressions.Expression)) &&
                 typeof(System.Linq.Expressions.Expression).IsAssignableFrom(itemType))
        {
            forbidden = true;
        }
        else
        {
            // 5) base/interface fallback
            for (Type t = itemType.BaseType; t != null && !forbidden; t = t.BaseType)
            {
                if (settings.forbiddenTypes.Contains(t)) forbidden = true;
            }

            if (!forbidden)
            {
                foreach (var i in itemType.GetInterfaces())
                {
                    if (settings.forbiddenTypes.Contains(i) ||
                        (i.IsGenericType && settings.forbiddenTypes.Contains(i.GetGenericTypeDefinition())))
                    {
                        forbidden = true;
                        break;
                    }
                }
            }
        }

        forbiddenTypeCache[itemType] = forbidden;
        return forbidden;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsIntrinsicSafeType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsPrimitive || type.IsEnum) return true;

        return type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type == typeof(IntPtr) ||
               type == typeof(UIntPtr) ||
               type == typeof(Uri) ||
               type == typeof(JsonFragment) ||
               type == typeof(ByteSegment) ||
               type == typeof(TextSegment) ||
               type == typeof(ArraySegment<byte>) ||
               type == typeof(byte[]);
    }

    private bool IsWhitelistedType(Type itemType)
    {
        if (IsIntrinsicSafeType(itemType)) return true;

        if (settings.allowedTypes.Contains(itemType)) return true;

        if (itemType.IsGenericType &&
            settings.allowedTypes.Contains(itemType.GetGenericTypeDefinition())) return true;

        string ns = itemType.Namespace ?? string.Empty;
        foreach (var prefix in settings.allowedNamespacePrefixes)
        {
            if (ns.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    TypeReaderInitializer CreateCustomTypeReader<T>(object customReaderObj, BaseTypeSettings typeSettings)
    {
        var customReader = (ICustomTypeReader<T>)customReaderObj;
        customReader.PrepareReader(this.preparationApi);
        var reader = () =>
        {
            return customReader.ReadValue(this.extensionApi);
        };
        Func<T, T> populatingReader = null;
        if (customReader.CanPopulateExistingValue)
        {
            populatingReader = (T itemToPopulate) =>
            {
                return customReader.ReadValue(this.extensionApi, itemToPopulate);
            };
        }
        // TODO: CustomTypes must be enabled to configure if children must write ref paths, but for now we assume that they do.        
        return TypeReaderInitializer.Create(this, reader, populatingReader, true, typeSettings);
    }

    private TypeReaderInitializer CreateByteArrayTypeReader(BaseTypeSettings typeSettings)
    {
        var byteTypeReader = GetCachedTypeReader(typeof(byte));
        var byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }, byteTypeReader.TypeSettings));
        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            return ReadByteArray(byteArrayReader);
        };

        return TypeReaderInitializer.Create(this, reader, null, false, typeSettings);
    }

    private TypeReaderInitializer CreateByteSegmentTypeReader(BaseTypeSettings typeSettings)
    {
        var byteTypeReader = GetCachedTypeReader(typeof(byte));
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }, byteTypeReader.TypeSettings));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ByteSegment) }, false, typeSettings));

        var reader = () =>
        {
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ByteSegment>();
            return new ByteSegment(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, true, typeSettings);
    }

    private TypeReaderInitializer CreateNullableByteSegmentTypeReader(BaseTypeSettings typeSettings)
    {
        var byteTypeReader = GetCachedTypeReader(typeof(byte));
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }, byteTypeReader.TypeSettings));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ByteSegment) }, false, typeSettings));

        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ByteSegment>();
            return (ByteSegment?)new ByteSegment(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, byteArrayReader.WriteRefPath, typeSettings);
    }

    private TypeReaderInitializer CreateByteArraySegmentTypeReader(BaseTypeSettings typeSettings)
    {
        var byteTypeReader = GetCachedTypeReader(typeof(byte));
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }, byteTypeReader.TypeSettings));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ArraySegment<byte>) }, false, typeSettings));

        var reader = () =>
        {
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ArraySegment<byte>>();
            return new ArraySegment<byte>(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, byteArrayReader.WriteRefPath, typeSettings);
    }

    private TypeReaderInitializer CreateNullableByteArraySegmentTypeReader(BaseTypeSettings typeSettings)
    {
        var byteTypeReader = GetCachedTypeReader(typeof(byte));
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }, byteTypeReader.TypeSettings));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ArraySegment<byte>) }, false, typeSettings));

        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ArraySegment<byte>>();
            return (ArraySegment<byte>?)new ArraySegment<byte>(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, byteArrayReader.WriteRefPath, typeSettings);
    }

    private TypeReaderInitializer CreateTextSegmentTypeReader(BaseTypeSettings typeSettings)
    {
        CachedTypeReader textSegmentObjectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(TextSegment) }, false, typeSettings));
        CachedTypeReader stringReader = GetCachedTypeReader(typeof(string));
        var reader = () =>
        {
            if (TryReadStringValueOrNull(out string s))
            {
                if (s == null) return default;
                return new TextSegment(s);
            }
            else
            {
                return textSegmentObjectReader.ReadValue_CheckProposed<TextSegment>();
            }
        };

        return TypeReaderInitializer.Create(this, reader, null, stringReader.WriteRefPath, typeSettings);
    }

    private TypeReaderInitializer CreateEnumReader<T>(BaseTypeSettings typeSettings) where T : struct, Enum
    {
        var reader = () =>
        {
            var valueType = Lookup(map_TypeStart, buffer.CurrentByte);
            if (valueType == TypeResult.Whitespace)
            {
                byte b = SkipWhiteSpaces();
                valueType = Lookup(map_TypeStart, b);
            }

            if (valueType == TypeResult.Number)
            {
                int i = ReadIntValue();
                if (!EnumHelper.TryFromInt(i, out T value)) throw new Exception("Invalid number for enum value");
                return value;
            }
            else if (valueType == TypeResult.String)
            {
                string s = ReadStringValue();
                if (!EnumHelper.TryFromString(s, out T value)) throw new Exception("Invalid string for enum value");
                return value;
            }
            else throw new Exception("Invalid character for determining enum value");
        };

        return TypeReaderInitializer.Create(this, reader, null, false, typeSettings);
    }

    private TypeReaderInitializer CreateNullableEnumReader<T>(BaseTypeSettings typeSettings) where T : struct, Enum
    {
        var reader = () =>
        {
            if (TryReadNullValue()) return null;
            if (!settings.strict && TryReadEmptyStringValue()) return null;

            var valueType = Lookup(map_TypeStart, buffer.CurrentByte);
            if (valueType == TypeResult.Whitespace)
            {
                byte b = SkipWhiteSpaces();
                valueType = Lookup(map_TypeStart, b);
            }

            if (valueType == TypeResult.Number)
            {
                int i = ReadIntValue();
                if (!EnumHelper.TryFromInt(i, out T value)) throw new Exception("Invalid number for enum value");
                return (T?)value;
            }
            else if (valueType == TypeResult.String)
            {
                string s = ReadStringValue();
                if (!EnumHelper.TryFromString(s, out T value)) throw new Exception("Invalid string for enum value");
                return (T?)value;
            }
            else throw new Exception("Invalid character for determining enum value");
        };

        return TypeReaderInitializer.Create(this, reader, null, false, typeSettings);
    }

    private bool TryCreateDictionaryTypeReader(Type itemType, BaseTypeSettings typeSettings, out TypeReaderInitializer initializer)
    {
        initializer = null;
        if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type keyType, out Type valueType)) return false;
        if (itemType.IsInterface)
        {
            if (itemType == typeof(IDictionary<,>))
            {
                itemType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            }
            else
            {
                //TODO: Find implementation for interface
                throw new NotImplementedException();
            }
        }

        initializer = this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateDictionaryTypeReader), [itemType, keyType, valueType], typeSettings);
        return true;
    }

    private TypeReaderInitializer CreateDictionaryTypeReader<T, K, V>(BaseTypeSettings typeSettings) where T : IDictionary<K, V>, new()
    {
        var keyReader = GetCachedTypeReader(typeof(K));
        var valueReader = GetCachedTypeReader(typeof(V));

        if (typeof(T).IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping or multi option type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, valueReader.WriteRefPath, typeSettings);
        }

        var constructor = GetConstructor<T>(null, typeSettings);
        var elementReader = new ElementReader<KeyValuePair<K, V>>(this);        
        var keyValuePairReader = GetCachedTypeReader(typeof(KeyValuePair<K, V>));
        bool isValueRefType = typeof(V).IsByRef;
        bool canValueBePopulated = CanTypeBePopulated(typeof(V));
        List<K> keysToKeep = new List<K>();
        ByteSegment keyProperty = new ByteSegment("Key".ToByteArray(), true);
        ByteSegment keyField = new ByteSegment("key".ToByteArray(), true);
        ByteSegment valueProperty = new ByteSegment("Value".ToByteArray(), true);
        ByteSegment valueField = new ByteSegment("value".ToByteArray(), true);

        // We avoid static for now because of the access to so many locals, but we could consider to make the reader static
        // and pass those as context if we want to optimize this further (TODO: Benchmark this against a static reader with context)
        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            var b = buffer.CurrentByte;
            T dict = constructor();
            if (b == '{')
            {
                if (!buffer.TryNextByte()) throw new Exception("Failed reading Object to Dictionary");
                while (true)
                {
                    b = SkipWhiteSpaces();
                    if (b == '}') break;

                    K fieldName = keyReader.ReadFieldName<K>(out var fieldNameBytes);
                    b = SkipWhiteSpaces();
                    if (b != ':') throw new Exception("Failed reading object to Dictionary");
                    buffer.TryNextByte();
                    V value = valueReader.ReadFieldValue<V>(fieldNameBytes);
                    dict[fieldName] = value;
                    b = SkipWhiteSpaces();
                    if (b == ',') buffer.TryNextByte();
                }

                if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading object to Dictionary");
            }
            else if (b == '[')
            {
                if (!buffer.TryNextByte()) throw new Exception("Failed reading Array to Dictionary");
                foreach (var element in elementReader)
                {
                    dict.Add(element.Key, element.Value);
                }
                if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array to Dictionary");
                buffer.TryNextByte();
            }
            else throw new Exception("Failed reading Dictionary");

            return dict;
        };

        Func<T, T> populatingReader = null;

        if (canValueBePopulated)
        {
            List<KeyValuePair<K, V>> keyValueList = new();
            populatingReader = (T itemToPopulate) =>
            {
                if (TryReadNullValue()) return default;
                var b = buffer.CurrentByte;
                T dict = itemToPopulate;
                try
                {
                    if (b == '{')
                    {
                        if (!buffer.TryNextByte()) throw new Exception("Failed reading Object to Dictionary");
                        while (true)
                        {
                            b = SkipWhiteSpaces();
                            if (b == '}') break;

                            K fieldName = keyReader.ReadFieldName<K>(out var fieldNameBytes);
                            keysToKeep.Add(fieldName);
                            b = SkipWhiteSpaces();
                            if (b != ':') throw new Exception("Failed reading object to Dictionary");
                            buffer.TryNextByte();
                            if (dict.TryGetValue(fieldName, out V value)) value = valueReader.ReadFieldValue<V>(fieldNameBytes, value);
                            else value = valueReader.ReadFieldValue<V>(fieldNameBytes);
                            keyValueList.Add(new KeyValuePair<K, V>(fieldName, value));
                            b = SkipWhiteSpaces();
                            if (b == ',') buffer.TryNextByte();
                        }

                        if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading object to Dictionary");
                    }
                    else if (buffer.CurrentByte == '[')
                    {
                        if (!buffer.TryNextByte()) throw new Exception("Failed reading Array to Dictionary");
                        while (true)
                        {
                            b = SkipWhiteSpaces();
                            if (b == ']') break;
                            buffer.TryNextByte();

                            // Look for start of KeyValuePair object
                            b = SkipWhiteSpaces();
                            if (b != '{') throw new Exception("Failed reading KeyValuePair for Dictionary");
                            buffer.TryNextByte();

                            // Look for field name "Key" and read its value
                            var keyFieldName = ReadStringBytes();
                            if (keyFieldName != keyField && keyFieldName != keyProperty) throw new Exception("Failed reading KeyValuePair for Dictionary");
                            b = SkipWhiteSpaces();
                            if (b != ':') throw new Exception("Failed reading KeyValuePair for Dictionary");
                            buffer.TryNextByte();
                            K fieldName = keyReader.ReadFieldName<K>(out var fieldNameBytes);

                            // next element
                            b = SkipWhiteSpaces();
                            if (b != ',') throw new Exception("Failed reading KeyValuePair for Dictionary");
                            buffer.TryNextByte();

                            // Look for field name "Value"
                            var valueFieldName = ReadStringBytes();
                            if (valueFieldName != valueField && valueFieldName != valueProperty) throw new Exception("Failed reading KeyValuePair for Dictionary");
                            b = SkipWhiteSpaces();
                            if (b != ':') throw new Exception("Failed reading KeyValuePair for Dictionary");
                            buffer.TryNextByte();
                            // If the field name exists in dictionary, populate its value, otherwise add it as new 
                            if (dict.TryGetValue(fieldName, out V value)) value = valueReader.ReadFieldValue<V>(fieldNameBytes, value);
                            else value = valueReader.ReadFieldValue<V>(fieldNameBytes);
                            keyValueList.Add(new KeyValuePair<K, V>(fieldName, value));

                            // Look for end of KEyValuePair object
                            b = SkipWhiteSpaces();
                            if (b != '}') throw new Exception("Failed reading KeyValuePair for Dictionary");
                            buffer.TryNextByte();

                            // Look for comma in case of next KeyValuePair
                            b = SkipWhiteSpaces();
                            if (b == ',') buffer.TryNextByte();
                        }
                        if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array to Dictionary");
                        buffer.TryNextByte();
                    }
                    else throw new Exception("Failed reading Dictionary");

                }
                finally
                {
                    dict.Clear();
                    for (int i = 0; i < keyValueList.Count; i++)
                    {
                        dict.Add(keyValueList[i]);
                    }
                    keyValueList.Clear();
                }

                return dict;
            };
        }
        else
        {
            populatingReader = (T itemToPopulate) =>
            {
                if (TryReadNullValue()) return default;
                var b = buffer.CurrentByte;

                T dict = itemToPopulate;
                dict.Clear();

                if (b == '{')
                {
                    if (!buffer.TryNextByte()) throw new Exception("Failed reading Object to Dictionary");
                    while (true)
                    {
                        b = SkipWhiteSpaces();
                        if (b == '}') break;

                        K fieldName = keyReader.ReadFieldName<K>(out var fieldNameBytes);
                        keysToKeep.Add(fieldName);
                        b = SkipWhiteSpaces();
                        if (b != ':') throw new Exception("Failed reading object to Dictionary");
                        buffer.TryNextByte();
                        V value = valueReader.ReadFieldValue<V>(fieldNameBytes);
                        dict[fieldName] = value;
                        b = SkipWhiteSpaces();
                        if (b == ',') buffer.TryNextByte();
                    }

                    if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading object to Dictionary");
                }
                else if (buffer.CurrentByte == '[')
                {
                    if (!buffer.TryNextByte()) throw new Exception("Failed reading Array to Dictionary");
                    foreach (var element in elementReader)
                    {
                        dict.Add(element.Key, element.Value);
                    }
                    if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array to Dictionary");
                    buffer.TryNextByte();
                }
                else throw new Exception("Failed reading Dictionary");

                return dict;
            };
        }

        return TypeReaderInitializer.Create(this, reader, populatingReader, valueReader.WriteRefPath, typeSettings);
    }

    private TypeReaderInitializer CreateUnknownObjectReader(CachedTypeReader cachedTypeReader, BaseTypeSettings typeSettings)
    {
        if (!settings.multiOptionTypeMapping.TryGetValue(typeof(object), out var typeOptions))
        {
            return TypeReaderInitializer.Create(this, ReadUnknownValue, null, true, typeSettings);
        }

        List<Type> objectTypeOptions = new List<Type>();
        Type arrayTypeOption = null;

        foreach (var typeOption in typeOptions)
        {
            if (typeOption.ImplementsInterface(typeof(IEnumerable)) || typeOption.ImplementsGenericInterface(typeof(IEnumerable<>)))
            {
                if (arrayTypeOption == null) arrayTypeOption = typeOption;
            }
            else if (!typeOption.IsPrimitive)
            {
                objectTypeOptions.Add(typeOption);
            }
        }

        if (arrayTypeOption == null && objectTypeOptions.Count == 0)
        {
            return TypeReaderInitializer.Create(this, ReadUnknownValue, null, true, typeSettings);
        }

        if (arrayTypeOption == null) arrayTypeOption = typeof(List<object>);
        objectTypeOptions.Add(typeof(Dictionary<string, object>));

        var arrayReader = GetCachedTypeReader(arrayTypeOption);
        CachedTypeReader objectReader = new CachedTypeReader((_) => CreateMultiOptionComplexTypeReader<object>(objectTypeOptions.ToArray(), typeSettings));

        var reader = () =>
        {
            var valueType = Lookup(map_TypeStart, buffer.CurrentByte);
            if (valueType == TypeResult.Whitespace)
            {
                byte b = SkipWhiteSpaces();
                valueType = Lookup(map_TypeStart, b);
            }

            switch (valueType)
            {
                case TypeResult.String: return ReadStringValue();
                case TypeResult.Object: return objectReader.ReadValue_CheckProposed<object>();
                case TypeResult.Bool: return ReadBoolValue();
                case TypeResult.Null: return ReadNullValue();
                case TypeResult.Array: return arrayReader.ReadValue_CheckProposed<object>();
                case TypeResult.Number: return ReadNumberValueAsObject();
                default: throw new Exception("Invalid character for determining value");
            }
        };

        return TypeReaderInitializer.Create(this, reader, null, true, typeSettings);

    }

    private TypeReaderInitializer CreateMultiOptionComplexTypeReader<T>(Type[] typeOptions, BaseTypeSettings typeSettings)
    {

        Type[] objectTypeOptions = typeOptions
            .Where(t => !t.IsPrimitive && !t.ImplementsGenericInterface(typeof(IDictionary<,>)))
            .ToArray();

        CachedTypeReader[] objectTypeReaders = objectTypeOptions
            .Select(t =>
            {
                if (typeof(T) == t)
                {
                    // If the type option is the same as the requested type, we have to create the type reader with CreateComplexTypeReader
                    // to avoid infinite recursion, because GetCachedTypeReader would return the currently created type reader which is not yet
                    // fully initialized and would cause infinite recursion when we try to read a value with it.
                    return new CachedTypeReader((_) => CreateComplexTypeReader<T>(false, typeSettings));
                }
                else return GetCachedTypeReader(t);
            })
            .ToArray();

        Type dictType = typeOptions.FirstOrDefault(t => t.ImplementsGenericInterface(typeof(IDictionary<,>)));
        CachedTypeReader dictTypeReader = dictType != null ? GetCachedTypeReader(dictType) : null;

        int numOptions = typeOptions.Length;

        Dictionary<ByteSegment, List<bool>> fieldNameToIsTypeMember = new();
        for (int i = 0; i < objectTypeOptions.Length; i++)
        {
            var typeOption = objectTypeOptions[i];
            var memberInfos = CreateMemberInfosList(typeOption, typeSettings?.dataAccess ?? settings.dataAccess);

            foreach (var memberInfo in memberInfos)
            {
                string name = memberInfo.Name;
                var itemFieldName = new ByteSegment(name.ToByteArray(), true);
                if (!fieldNameToIsTypeMember.TryGetValue(itemFieldName, out var indicesList))
                {
                    indicesList = Enumerable.Repeat(false, objectTypeOptions.Length).ToList();
                    fieldNameToIsTypeMember[itemFieldName] = indicesList;
                }
                indicesList[i] = true;

                if (name.TryExtract("<{name}>k__BackingField", out string propertyName))
                {
                    name = propertyName;
                    itemFieldName = new ByteSegment(name.ToByteArray(), true);
                    if (!fieldNameToIsTypeMember.TryGetValue(itemFieldName, out indicesList))
                    {
                        indicesList = Enumerable.Repeat(false, objectTypeOptions.Length).ToList();
                        fieldNameToIsTypeMember[itemFieldName] = indicesList;
                    }
                    indicesList[i] = true;
                }
            }

            // Mark all fields that are equally available for all types
            foreach (var pair in fieldNameToIsTypeMember)
            {
                if (pair.Value.All(v => v == true)) fieldNameToIsTypeMember[pair.Key] = null;
            }
        }

        Pool<List<int>> ratingsPool = new Pool<List<int>>(() => new(), l => l.Clear(), 1000, false);

        var reader = () =>
        {
            byte b = SkipWhiteSpaces();
            var ratings = ratingsPool.Take();
            ratings.AddRange(Enumerable.Repeat(0, numOptions));

            if (b != '{') throw new Exception("Failed reading object");
            int selectionIndex = -1;
            int fallbackIndex = -1;
            int selectionRating = 0;

            // We always undo the read operations for determining the type,
            // so that we can read the object again with the correct type reader after determining the type based on the fields.
            // This is necessary, because we might have read some fields already when we find a field that is only available
            // in one of the types, but we don't know which type it belongs to until we read the field name.
            using (CreateUndoReadHandle())
            {
                buffer.TryNextByte();

                while (true)
                {
                    b = SkipWhiteSpaces();
                    if (b == '}') break;

                    var fieldName = ReadStringBytes();
                    if (fieldNameToIsTypeMember.TryGetValue(fieldName, out var typeIndices))
                    {
                        if (typeIndices == null)
                        {
                            // If field is available for every type and no type is yet fallback, set the first as fallback
                            if (fallbackIndex == -1) fallbackIndex = 0;
                        }
                        else
                        {
                            for (var i = 0; i < typeIndices.Count; i++)
                            {
                                if (typeIndices[i])
                                {
                                    int rating = ratings[i];
                                    rating++;
                                    ratings[i] = rating;
                                    if (selectionIndex == i)
                                    {
                                        selectionRating = rating;
                                    }
                                    else if (rating == selectionRating)
                                    {
                                        selectionRating = rating;
                                        if (selectionIndex != -1) fallbackIndex = selectionIndex;
                                        selectionIndex = -1;
                                    }
                                    else if (rating > selectionRating)
                                    {
                                        selectionRating = rating;
                                        selectionIndex = i;
                                    }
                                }
                            }
                        }
                    }

                    if (selectionIndex != -1) break;

                    b = SkipWhiteSpaces();
                    if (b != ':') throw new Exception("Failed reading object");
                    buffer.TryNextByte();
                    SkipValue();
                    b = SkipWhiteSpaces();
                    if (b == ',') buffer.TryNextByte();
                }
                ratingsPool.Return(ratings);
            }

            if (selectionIndex == -1)
            {
                if (fallbackIndex != -1)
                {
                    selectionIndex = fallbackIndex;
                }
                else if (dictTypeReader != null)
                {
                    return dictTypeReader.ReadValue_CheckProposed<T>();
                }
                else
                {
                    SkipObject();
                    return default;
                }
            }
            return objectTypeReaders[selectionIndex].ReadValue_CheckProposed<T>();
        };

        return TypeReaderInitializer.Create(this, reader, null, true, typeSettings);
    }

    private TypeReaderInitializer CreateComplexTypeReader<T>(bool checkForMultiOptions, BaseTypeSettings typeSettings)
    {
        Type itemType = typeof(T);

        if (checkForMultiOptions && settings.multiOptionTypeMapping.TryGetValue(itemType, out Type[] mappedTypeOptions))
        {
            return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateMultiOptionComplexTypeReader), [itemType], [mappedTypeOptions, typeSettings]);
        }

        if (itemType.IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping or multi option type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, true, typeSettings);
        }
        List<MemberInfo> memberInfos = CreateMemberInfosList(itemType, typeSettings?.dataAccess ?? settings.dataAccess);

        Dictionary<ByteSegment, int> itemFieldWritersIndexLookup = new();
        List<(ByteSegment name, Func<T, T> itemFieldWriter)> itemFieldWritersList = new();
        bool childrenMustWriteRefPath = false;
        foreach (var memberInfo in memberInfos)
        {
            ByteSegment itemFieldName;
            Func<T, T> itemFieldWriter;

            Type fieldType = GetFieldOrPropertyType(memberInfo);
            string name = memberInfo.Name;
            if (!name.TryExtract("<{name}>k__BackingField", out string propertyName)) propertyName = null;
            Settings.BackingFieldMode backingFieldMode = typeSettings?.backingFieldMode ?? settings.backingFieldMode;

            BaseTypeSettings memberSettings = null;
            if (typeSettings != null &&
                (typeSettings.memberSettingsDict.TryGetValue(name, out memberSettings) ||
                 typeSettings.memberSettingsDict.TryGetValue(propertyName ?? name, out memberSettings)))
            {
                if (memberSettings.member_ignore == true) continue;
                if (memberSettings.member_overrideName != null)
                {
                    // If there is an override name, we ignore the propertyName,
                    // because otherwise we would create two field writers for the same name.
                    name = memberSettings.member_overrideName;
                    propertyName = null;
                    backingFieldMode = Settings.BackingFieldMode.TryBackingFieldNameOnly;
                }
            }

            if (backingFieldMode != Settings.BackingFieldMode.TryPropertyNameOnly)
            {
                itemFieldName = new ByteSegment(name.ToByteArray(), true);
                itemFieldWriter = this.InvokeGenericMethod<Func<T, T>>(nameof(CreateItemFieldWriter), new Type[] { itemType, fieldType, itemType }, memberInfo, itemFieldName, memberSettings);
                itemFieldWritersIndexLookup[itemFieldName] = itemFieldWritersList.Count;
                itemFieldWritersList.Add((itemFieldName, itemFieldWriter));
            }
            if (propertyName != null && backingFieldMode != Settings.BackingFieldMode.TryBackingFieldNameOnly)
            {                
                name = propertyName;
                itemFieldName = new ByteSegment(name.ToByteArray(), true);
                itemFieldWriter = this.InvokeGenericMethod<Func<T, T>>(nameof(CreateItemFieldWriter), new Type[] { itemType, fieldType, itemType }, memberInfo, itemFieldName, memberSettings);
                itemFieldWritersIndexLookup[itemFieldName] = itemFieldWritersList.Count;
                itemFieldWritersList.Add((itemFieldName, itemFieldWriter));
            }

            if (!childrenMustWriteRefPath && GetCachedTypeReader(fieldType).WriteRefPath) childrenMustWriteRefPath = true;
        }
        int writerCount = itemFieldWritersList.Count;
        var itemFieldWriters = itemFieldWritersList.ToArray();
        itemFieldWritersList = null;
        var constructor = GetConstructor<T>(null, typeSettings);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryFindFieldWriter(ByteSegment fieldName, ref int expectedFieldIndex, out Func<T, T> fieldWriter)
        {
            if (writerCount == 0)
            {
                fieldWriter = null;
                return false;
            }
            // Check the expected field index first, because in most cases the fields will be in the same order as they are
            // defined in the type, so we can avoid the dictionary lookup in most cases.
            (ByteSegment name, fieldWriter) = itemFieldWriters[expectedFieldIndex];
            if (name == fieldName)
            {
                expectedFieldIndex++;
                if (expectedFieldIndex >= writerCount) expectedFieldIndex = 0;
                return true;
            }

            // Try another quick check at the next index, because there might be a single field missing
            (name, fieldWriter) = itemFieldWriters[expectedFieldIndex];
            if (name == fieldName)
            {
                expectedFieldIndex++;
                if (expectedFieldIndex >= writerCount) expectedFieldIndex = 0;
                return true;
            }

            // If the quick checks fail, we have to do the dictionary lookup
            fieldName.EnsureHashCode();
            if (itemFieldWritersIndexLookup.TryGetValue(fieldName, out int index))
            {
                (_, fieldWriter) = itemFieldWriters[index];
                expectedFieldIndex = index + 1;
                if (expectedFieldIndex >= writerCount) expectedFieldIndex = 0;
                return true;
            }
            return false;
        }

        // Cannot make it static, because it uses the TryFindFieldWriter local function
        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            var b = buffer.CurrentByte;

            T item = constructor();
            int expectedFieldIndex = 0;

            if (b != '{') throw new Exception("Failed reading object");
            buffer.TryNextByte();

            while (true)
            {
                b = SkipWhiteSpaces();
                if (b == '}') break;

                var fieldName = ReadStringBytes();
                b = SkipWhiteSpaces();
                if (b != ':') throw new Exception("Failed reading object");
                buffer.TryNextByte();
                if (TryFindFieldWriter(fieldName, ref expectedFieldIndex, out var fieldWriter)) item = fieldWriter.Invoke(item);
                else SkipValue();
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();
            }

            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading object");
            return item;
        };

        // Cannot make it static, because it uses the TryFindFieldWriter local function
        var populatingReader = (T itemToPopulate) =>
        {
            if (TryReadNullValue()) return default;
            var b = buffer.CurrentByte;

            T item = itemToPopulate;
            int expectedFieldIndex = 0;

            if (b != '{') throw new Exception("Failed reading object");
            buffer.TryNextByte();

            while (true)
            {
                b = SkipWhiteSpaces();
                if (b == '}') break;

                var fieldName = ReadStringBytes();
                b = SkipWhiteSpaces();
                if (b != ':') throw new Exception("Failed reading object");
                buffer.TryNextByte();
                if (TryFindFieldWriter(fieldName, ref expectedFieldIndex, out var fieldWriter)) item = fieldWriter.Invoke(item);
                else SkipValue();
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();
            }

            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading object");
            return item;
        };

        return TypeReaderInitializer.Create(this, reader, populatingReader, childrenMustWriteRefPath, typeSettings);
    }

    private List<MemberInfo> CreateMemberInfosList(Type itemType, DataAccess dataAccess)
    {
        var memberInfos = new List<MemberInfo>();
        if (dataAccess == DataAccess.PublicFieldsAndProperties)
        {
            memberInfos.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.SetMethod != null && !prop.IsDefined(typeof(JsonIgnoreAttribute), true)));
            memberInfos.AddRange(itemType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => !field.IsDefined(typeof(JsonIgnoreAttribute), true)));

            memberInfos.AddRange(itemType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(prop => prop.SetMethod != null && prop.IsDefined(typeof(JsonIncludeAttribute), true)));
            memberInfos.AddRange(itemType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.IsDefined(typeof(JsonIncludeAttribute), true)));
        }
        else
        {
            memberInfos.AddRange(itemType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Where(field => !field.IsDefined(typeof(JsonIgnoreAttribute), true)));
            Type t = itemType.BaseType;
            while (t != null)
            {
                memberInfos.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(baseField => !baseField.IsDefined(typeof(JsonIgnoreAttribute), true) && !memberInfos.Any(field => field.Name == baseField.Name)));
                t = t.BaseType;
            }

            memberInfos.AddRange(itemType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => prop.IsDefined(typeof(JsonIncludeAttribute), true)));
            t = itemType.BaseType;
            while (t != null)
            {
                memberInfos.AddRange(t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(baseProp => baseProp.IsDefined(typeof(JsonIncludeAttribute), true) && !memberInfos.Any(field => field.Name == baseProp.Name)));
                t = t.BaseType;
            }
        }

        return memberInfos;
    }

    private Func<C, C> CreateItemFieldWriter<T, V, C>(MemberInfo memberInfo, ByteSegment fieldName, BaseTypeSettings memberSettings) where T : C
    {
        Type itemType = typeof(T);
        Type fieldType = typeof(V);

        if (memberInfo is FieldInfo fieldInfo)
        {
            if (fieldInfo.IsInitOnly) // Check if the field is read-only
            {
                // Handle read-only fields
                return CreateFieldWriterForInitOnlyFields<T, V, C>(fieldInfo, fieldName, memberSettings);
            }
            else
            {
                // Use expression tree for normal writable field
                return CreateFieldWriterUsingExpression<T, V, C>(fieldInfo, fieldName, memberSettings);
            }
        }
        else if (memberInfo is PropertyInfo propertyInfo)
        {
            if (propertyInfo.CanWrite)
            {
                // Includes init-only properties in current runtime behavior:
                // init accessors are reported as writable here.
                // Use expression tree for writable property
                return CreatePropertyWriterUsingExpression<T, V, C>(propertyInfo, fieldName, memberSettings);
            }
        }

        throw new InvalidOperationException("MemberInfo must be a writable field, property, or init-only property.");
    }

    private Func<C, C> CreateFieldWriterForInitOnlyFields<T, V, C>(FieldInfo fieldInfo, ByteSegment fieldName, BaseTypeSettings memberSettings) where T : C
    {
        Type itemType = typeof(T);
        Type fieldType = typeof(V);
        var fieldTypeReader = GetCachedTypeReader(fieldType, memberSettings);

        if (itemType.IsValueType)
        {
            if (fieldTypeReader.CanBePopulated)
            {
                return parentItem =>
                {
                    V value = (V)fieldInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                    value = fieldTypeReader.ReadFieldValue<V>(fieldName, value);
                    var boxedItem = (object)parentItem;
                    fieldInfo.SetValue(boxedItem, value);
                    parentItem = (T)boxedItem;
                    return parentItem;
                };
            }
            else
            {
                if (fieldTypeReader.IsNoCheckPossible<V>())
                {
                    if (typeof(V) == typeof(string))
                    {
                        if (CheckUseStringCache(memberSettings)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, StringReader_WithStringCache_Strategy, string>(fieldInfo, fieldTypeReader);
                        else return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, StringReader_WithoutStringCache_Strategy, string>(fieldInfo, fieldTypeReader);
                    }
                    if (typeof(V) == typeof(char)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, CharReaderStrategy, char>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(sbyte)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, SByteReaderStrategy, sbyte>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(byte)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, ByteReaderStrategy, byte>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(short)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, Int16ReaderStrategy, short>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(ushort)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, UInt16ReaderStrategy, ushort>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(int)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, Int32ReaderStrategy, int>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(uint)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, UInt32ReaderStrategy, uint>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(long)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, Int64ReaderStrategy, long>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(ulong)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, UInt64ReaderStrategy, ulong>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(float)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, FloatReaderStrategy, float>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(double)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, DoubleReaderStrategy, double>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(bool)) return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, BoolReaderStrategy, bool>(fieldInfo, fieldTypeReader);
                    return CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, GenericReaderStrategy<V>, V>(fieldInfo, fieldTypeReader);
                }
                else
                {
                    return parentItem =>
                    {
                        V value = fieldTypeReader.ReadFieldValue<V>(fieldName);
                        var boxedItem = (object)parentItem;
                        fieldInfo.SetValue(boxedItem, value);
                        parentItem = (T)boxedItem;
                        return parentItem;
                    };
                }
            }
        }
        else
        {
            if (fieldTypeReader.CanBePopulated)
            {
                return parentItem =>
                {
                    V value = (V)fieldInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                    value = fieldTypeReader.ReadFieldValue<V>(fieldName, value);
                    fieldInfo.SetValue(parentItem, value);
                    return parentItem;
                };
            }
            else
            {
                if (fieldTypeReader.IsNoCheckPossible<V>())
                {
                    if (typeof(V) == typeof(string))
                    {
                        if (CheckUseStringCache(memberSettings)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, StringReader_WithStringCache_Strategy, string>(fieldInfo, fieldTypeReader);
                        else return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, StringReader_WithoutStringCache_Strategy, string>(fieldInfo, fieldTypeReader);
                    }
                    if (typeof(V) == typeof(char)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, CharReaderStrategy, char>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(sbyte)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, SByteReaderStrategy, sbyte>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(byte)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, ByteReaderStrategy, byte>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(short)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, Int16ReaderStrategy, short>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(ushort)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, UInt16ReaderStrategy, ushort>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(int)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, Int32ReaderStrategy, int>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(uint)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, UInt32ReaderStrategy, uint>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(long)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, Int64ReaderStrategy, long>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(ulong)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, UInt64ReaderStrategy, ulong>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(float)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, FloatReaderStrategy, float>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(double)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, DoubleReaderStrategy, double>(fieldInfo, fieldTypeReader);
                    if (typeof(V) == typeof(bool)) return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, BoolReaderStrategy, bool>(fieldInfo, fieldTypeReader);
                    return CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, GenericReaderStrategy<V>, V>(fieldInfo, fieldTypeReader);
                }
                else
                {
                    return parentItem =>
                    {
                        V value = fieldTypeReader.ReadFieldValue<V>(fieldName);
                        fieldInfo.SetValue(parentItem, value);
                        return parentItem;
                    };
                }
            }
        }
    }

    private Func<C, C> CreateFieldWriterUsingExpression<T, V, C>(FieldInfo fieldInfo, ByteSegment fieldName, BaseTypeSettings memberSettings) where T : C
    {
        Type itemType = typeof(T);
        Type fieldType = typeof(V);
        var fieldTypeReader = GetCachedTypeReader(fieldType, memberSettings);

        var target = Expression.Parameter(itemType, "target");
        var value = Expression.Parameter(fieldType, "value");
        var memberAccess = Expression.Field(target, fieldInfo);

        if (itemType.IsValueType)
        {
            // Create an expression to set the field value on the struct
            var assignExpression = Expression.Assign(memberAccess, value);

            // Create a block expression that modifies the struct
            var body = Expression.Block(
                assignExpression,
                target // Return the modified struct
            );

            var lambdaSetter = Expression.Lambda<Func<T, V, T>>(body, target, value);
            var setValueAndReturn = lambdaSetter.Compile();

            var lambdaGetter = Expression.Lambda<Func<T, V>>(memberAccess, target);
            Func<T, V> getValue = lambdaGetter.Compile();

            if (fieldTypeReader.CanBePopulated)
            {
                return parentItem =>
                {
                    V fieldValue = getValue((T)parentItem);
                    fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName, fieldValue);
                    parentItem = setValueAndReturn((T)parentItem, fieldValue);
                    return parentItem;
                };
            }
            else
            {
                if (fieldTypeReader.IsNoCheckPossible<V>())
                {
                    if (typeof(V) == typeof(string))
                    {
                        if (CheckUseStringCache(memberSettings)) return CreateValueFieldWriterViaStrategy<T, V, C, StringReader_WithStringCache_Strategy, string>(setValueAndReturn, fieldTypeReader);
                        else return CreateValueFieldWriterViaStrategy<T, V, C, StringReader_WithoutStringCache_Strategy, string>(setValueAndReturn, fieldTypeReader);
                    }
                    if (typeof(V) == typeof(char)) return CreateValueFieldWriterViaStrategy<T, V, C, CharReaderStrategy, char>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(sbyte)) return CreateValueFieldWriterViaStrategy<T, V, C, SByteReaderStrategy, sbyte>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(byte)) return CreateValueFieldWriterViaStrategy<T, V, C, ByteReaderStrategy, byte>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(short)) return CreateValueFieldWriterViaStrategy<T, V, C, Int16ReaderStrategy, short>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(ushort)) return CreateValueFieldWriterViaStrategy<T, V, C, UInt16ReaderStrategy, ushort>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(int)) return CreateValueFieldWriterViaStrategy<T, V, C, Int32ReaderStrategy, int>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(uint)) return CreateValueFieldWriterViaStrategy<T, V, C, UInt32ReaderStrategy, uint>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(long)) return CreateValueFieldWriterViaStrategy<T, V, C, Int64ReaderStrategy, long>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(ulong)) return CreateValueFieldWriterViaStrategy<T, V, C, UInt64ReaderStrategy, ulong>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(float)) return CreateValueFieldWriterViaStrategy<T, V, C, FloatReaderStrategy, float>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(double)) return CreateValueFieldWriterViaStrategy<T, V, C, DoubleReaderStrategy, double>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(bool)) return CreateValueFieldWriterViaStrategy<T, V, C, BoolReaderStrategy, bool>(setValueAndReturn, fieldTypeReader);
                    return CreateValueFieldWriterViaStrategy<T, V, C, GenericReaderStrategy<V>, V>(setValueAndReturn, fieldTypeReader);
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName);
                        parentItem = setValueAndReturn((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
            }
        }
        else
        {
            BinaryExpression assignExpression = Expression.Assign(memberAccess, value);
            var lambdaSetter = Expression.Lambda<Action<T, V>>(assignExpression, target, value);
            Action<T, V> setValue = lambdaSetter.Compile();

            var lambdaGetter = Expression.Lambda<Func<T, V>>(memberAccess, target);
            Func<T, V> getValue = lambdaGetter.Compile();
            if (fieldTypeReader.CanBePopulated)
            {
                return parentItem =>
                {
                    V fieldValue = getValue((T)parentItem);
                    fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName, fieldValue);
                    setValue((T)parentItem, fieldValue);
                    return parentItem;
                };
            }
            else
            {
                if (fieldTypeReader.IsNoCheckPossible<V>())
                {
                    if (typeof(V) == typeof(string))
                    {
                        if (CheckUseStringCache(memberSettings)) return CreateObjFieldWriterViaStrategy<T, V, C, StringReader_WithStringCache_Strategy, string>(setValue, fieldTypeReader);
                        else return CreateObjFieldWriterViaStrategy<T, V, C, StringReader_WithoutStringCache_Strategy, string>(setValue, fieldTypeReader);
                    }
                    if (typeof(V) == typeof(char)) return CreateObjFieldWriterViaStrategy<T, V, C, CharReaderStrategy, char>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(sbyte)) return CreateObjFieldWriterViaStrategy<T, V, C, SByteReaderStrategy, sbyte>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(byte)) return CreateObjFieldWriterViaStrategy<T, V, C, ByteReaderStrategy, byte>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(short)) return CreateObjFieldWriterViaStrategy<T, V, C, Int16ReaderStrategy, short>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(ushort)) return CreateObjFieldWriterViaStrategy<T, V, C, UInt16ReaderStrategy, ushort>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(int)) return CreateObjFieldWriterViaStrategy<T, V, C, Int32ReaderStrategy, int>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(uint)) return CreateObjFieldWriterViaStrategy<T, V, C, UInt32ReaderStrategy, uint>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(long)) return CreateObjFieldWriterViaStrategy<T, V, C, Int64ReaderStrategy, long>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(ulong)) return CreateObjFieldWriterViaStrategy<T, V, C, UInt64ReaderStrategy, ulong>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(float)) return CreateObjFieldWriterViaStrategy<T, V, C, FloatReaderStrategy, float>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(double)) return CreateObjFieldWriterViaStrategy<T, V, C, DoubleReaderStrategy, double>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(bool)) return CreateObjFieldWriterViaStrategy<T, V, C, BoolReaderStrategy, bool>(setValue, fieldTypeReader);
                    return CreateObjFieldWriterViaStrategy<T, V, C, GenericReaderStrategy<V>, V>(setValue, fieldTypeReader);
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName);
                        setValue((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
            }
        }
    }

    private bool CheckUseStringCache(BaseTypeSettings memberSettings)
    {
        if (memberSettings?.member_useStringCache == null) return useStringCache;
        return memberSettings.member_useStringCache.Value;
    }

    private Func<C, C> CreatePropertyWriterUsingExpression<T, V, C>(PropertyInfo propertyInfo, ByteSegment fieldName, BaseTypeSettings memberSettings) where T : C
    {
        Type itemType = typeof(T);
        Type fieldType = typeof(V);

        var fieldTypeReader = GetCachedTypeReader(fieldType, memberSettings);

        var target = Expression.Parameter(itemType, "target");
        var value = Expression.Parameter(fieldType, "value");
        var memberAccess = Expression.Property(target, propertyInfo);

        if (itemType.IsValueType)
        {
            // Create an expression to set the field value on the struct
            var assignExpression = Expression.Assign(memberAccess, value);

            // Create a block expression that modifies the struct
            var body = Expression.Block(
                assignExpression,
                target // Return the modified struct
            );

            var lambdaSetter = Expression.Lambda<Func<T, V, T>>(body, target, value);
            var setValueAndReturn = lambdaSetter.Compile();

            var lambdaGetter = Expression.Lambda<Func<T, V>>(memberAccess, target);
            Func<T, V> getValue = lambdaGetter.Compile();

            if (fieldTypeReader.CanBePopulated && propertyInfo.CanRead)
            {
                return parentItem =>
                {
                    V fieldValue = getValue((T)parentItem);
                    fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName, fieldValue);
                    parentItem = setValueAndReturn((T)parentItem, fieldValue);
                    return parentItem;
                };
            }
            else
            {
                if (fieldTypeReader.IsNoCheckPossible<V>())
                {

                    if (typeof(V) == typeof(string))
                    {
                        if (CheckUseStringCache(memberSettings)) return CreateValueFieldWriterViaStrategy<T, V, C, StringReader_WithStringCache_Strategy, string>(setValueAndReturn, fieldTypeReader);
                        else return CreateValueFieldWriterViaStrategy<T, V, C, StringReader_WithoutStringCache_Strategy, string>(setValueAndReturn, fieldTypeReader);
                    }
                    if (typeof(V) == typeof(char)) return CreateValueFieldWriterViaStrategy<T, V, C, CharReaderStrategy, char>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(sbyte)) return CreateValueFieldWriterViaStrategy<T, V, C, SByteReaderStrategy, sbyte>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(byte)) return CreateValueFieldWriterViaStrategy<T, V, C, ByteReaderStrategy, byte>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(short)) return CreateValueFieldWriterViaStrategy<T, V, C, Int16ReaderStrategy, short>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(ushort)) return CreateValueFieldWriterViaStrategy<T, V, C, UInt16ReaderStrategy, ushort>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(int)) return CreateValueFieldWriterViaStrategy<T, V, C, Int32ReaderStrategy, int>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(uint)) return CreateValueFieldWriterViaStrategy<T, V, C, UInt32ReaderStrategy, uint>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(long)) return CreateValueFieldWriterViaStrategy<T, V, C, Int64ReaderStrategy, long>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(ulong)) return CreateValueFieldWriterViaStrategy<T, V, C, UInt64ReaderStrategy, ulong>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(float)) return CreateValueFieldWriterViaStrategy<T, V, C, FloatReaderStrategy, float>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(double)) return CreateValueFieldWriterViaStrategy<T, V, C, DoubleReaderStrategy, double>(setValueAndReturn, fieldTypeReader);
                    if (typeof(V) == typeof(bool)) return CreateValueFieldWriterViaStrategy<T, V, C, BoolReaderStrategy, bool>(setValueAndReturn, fieldTypeReader);
                    return CreateValueFieldWriterViaStrategy<T, V, C, GenericReaderStrategy<V>, V>(setValueAndReturn, fieldTypeReader);
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName);
                        parentItem = setValueAndReturn((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
            }
        }
        else
        {
            BinaryExpression assignExpression = Expression.Assign(memberAccess, value);
            var lambdaSetter = Expression.Lambda<Action<T, V>>(assignExpression, target, value);
            Action<T, V> setValue = lambdaSetter.Compile();

            var lambdaGetter = Expression.Lambda<Func<T, V>>(memberAccess, target);
            Func<T, V> getValue = lambdaGetter.Compile();

            if (fieldTypeReader.CanBePopulated && propertyInfo.CanRead)
            {
                return parentItem =>
                {
                    V fieldValue = getValue((T)parentItem);
                    fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName, fieldValue);
                    setValue((T)parentItem, fieldValue);
                    return parentItem;
                };
            }
            else
            {
                if (fieldTypeReader.IsNoCheckPossible<V>())
                {
                    if (typeof(V) == typeof(string))
                    {
                        if (CheckUseStringCache(memberSettings)) return CreateObjFieldWriterViaStrategy<T, V, C, StringReader_WithStringCache_Strategy, string>(setValue, fieldTypeReader);
                        else return CreateObjFieldWriterViaStrategy<T, V, C, StringReader_WithoutStringCache_Strategy, string>(setValue, fieldTypeReader);
                    }
                    if (typeof(V) == typeof(char)) return CreateObjFieldWriterViaStrategy<T, V, C, CharReaderStrategy, char>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(sbyte)) return CreateObjFieldWriterViaStrategy<T, V, C, SByteReaderStrategy, sbyte>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(byte)) return CreateObjFieldWriterViaStrategy<T, V, C, ByteReaderStrategy, byte>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(short)) return CreateObjFieldWriterViaStrategy<T, V, C, Int16ReaderStrategy, short>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(ushort)) return CreateObjFieldWriterViaStrategy<T, V, C, UInt16ReaderStrategy, ushort>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(int)) return CreateObjFieldWriterViaStrategy<T, V, C, Int32ReaderStrategy, int>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(uint)) return CreateObjFieldWriterViaStrategy<T, V, C, UInt32ReaderStrategy, uint>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(long)) return CreateObjFieldWriterViaStrategy<T, V, C, Int64ReaderStrategy, long>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(ulong)) return CreateObjFieldWriterViaStrategy<T, V, C, UInt64ReaderStrategy, ulong>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(float)) return CreateObjFieldWriterViaStrategy<T, V, C, FloatReaderStrategy, float>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(double)) return CreateObjFieldWriterViaStrategy<T, V, C, DoubleReaderStrategy, double>(setValue, fieldTypeReader);
                    if (typeof(V) == typeof(bool)) return CreateObjFieldWriterViaStrategy<T, V, C, BoolReaderStrategy, bool>(setValue, fieldTypeReader);
                    return CreateObjFieldWriterViaStrategy<T, V, C, GenericReaderStrategy<V>, V>(setValue, fieldTypeReader);
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadFieldValue<V>(fieldName);
                        setValue((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
            }
        }
    }

    private TypeReaderInitializer CreateArrayTypeReader(Type arrayType, CachedTypeReader cachedTypeReader, BaseTypeSettings typeSettings)
    {
        if (arrayType == typeof(byte[]))
        {
            return CreateByteArrayTypeReader(typeSettings);
        }
        else
        {
            return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { arrayType.GetElementType() }, typeSettings);
        }
    }

    private TypeReaderInitializer CreateGenericArrayTypeReader<E>(BaseTypeSettings typeSettings)
    {
        var pool = new Pool<List<E>>(() => new List<E>(), l => l.Clear(), 10, false);

        var elementTypeReader = GetCachedTypeReader(typeof(E));
        if (elementTypeReader.IsNoCheckPossible<E>())
        {
            if (typeof(E) == typeof(string))
            {
                if (CheckUseStringCache(typeSettings)) return CreateGenericArrayTypeReaderViaStrategy<E, StringReader_WithStringCache_Strategy, string>(elementTypeReader, pool, typeSettings);
                else return CreateGenericArrayTypeReaderViaStrategy<E, StringReader_WithoutStringCache_Strategy, string>(elementTypeReader, pool, typeSettings);
            }
            if (typeof(E) == typeof(char)) return CreateGenericArrayTypeReaderViaStrategy<E, CharReaderStrategy, char>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(sbyte)) return CreateGenericArrayTypeReaderViaStrategy<E, SByteReaderStrategy, sbyte>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(byte)) return CreateGenericArrayTypeReaderViaStrategy<E, ByteReaderStrategy, byte>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(short)) return CreateGenericArrayTypeReaderViaStrategy<E, Int16ReaderStrategy, short>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(ushort)) return CreateGenericArrayTypeReaderViaStrategy<E, UInt16ReaderStrategy, ushort>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(int)) return CreateGenericArrayTypeReaderViaStrategy<E, Int32ReaderStrategy, int>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(uint)) return CreateGenericArrayTypeReaderViaStrategy<E, UInt32ReaderStrategy, uint>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(long)) return CreateGenericArrayTypeReaderViaStrategy<E, Int64ReaderStrategy, long>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(ulong)) return CreateGenericArrayTypeReaderViaStrategy<E, UInt64ReaderStrategy, ulong>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(bool)) return CreateGenericArrayTypeReaderViaStrategy<E, BoolReaderStrategy, bool>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(float)) return CreateGenericArrayTypeReaderViaStrategy<E, FloatReaderStrategy, float>(elementTypeReader, pool, typeSettings);
            if (typeof(E) == typeof(double)) return CreateGenericArrayTypeReaderViaStrategy<E, DoubleReaderStrategy, double>(elementTypeReader, pool, typeSettings);
            return CreateGenericArrayTypeReaderViaStrategy<E, GenericReaderStrategy<E>, E>(elementTypeReader, pool, typeSettings);
        }
        else
        {
            var elementReaderPool = new Pool<ElementReader<E>>(() => new ElementReader<E>(this, elementTypeReader), l => l.Reset(), 10, false);

            var reader = () =>
            {
                byte b = SkipWhiteSpaces();
                if (b != '[') throw new Exception("Failed reading Array");
                if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
                List<E> elementBuffer = pool.Take();
                var elementReader = elementReaderPool.Take();
                elementBuffer.AddRange(elementReader);
                E[] item = elementBuffer.ToArray();
                if (elementTypeReader.ResolveRefPath) SetItemRefInCurrentItemInfo(item);
                pool.Return(elementBuffer);
                elementReaderPool.Return(elementReader);
                if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
                buffer.TryNextByte();
                return item;
            };

            return TypeReaderInitializer.Create(this, reader, null, elementTypeReader.WriteRefPath, typeSettings);
        }
    }

    private bool TryCreateEnumerableTypeReader(Type itemType, BaseTypeSettings typeSettings, out TypeReaderInitializer initializer)
    {
        if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType))
        {
            initializer = this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericEnumerableTypeReader), new Type[] { itemType, elementType }, typeSettings);
        }
        else if (itemType.ImplementsInterface(typeof(IEnumerable)))
        {
            initializer = this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateEnumerableTypeReader), new Type[] { itemType }, typeSettings);
        }
        else
        {
            initializer = null;
            return false;
        }

        return true;
    }



    private TypeReaderInitializer CreateGenericEnumerableTypeReader<T, E>(BaseTypeSettings typeSettings)
    {
        var elementTypeReader = GetCachedTypeReader(typeof(E));

        if (typeof(T).IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, elementTypeReader.WriteRefPath, typeSettings);
        }

        Func<IEnumerable<E>, T> constructor = GetConstructor<T, IEnumerable<E>>(typeSettings);        
        Pool<List<E>> bufferPool = new Pool<List<E>>(() => new List<E>(), l => l.Clear(), 10, false);

        if (elementTypeReader.IsNoCheckPossible<E>())
        {
            if (typeof(E) == typeof(string))
            {
                if (CheckUseStringCache(typeSettings)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, StringReader_WithStringCache_Strategy, string>(elementTypeReader, constructor, bufferPool, typeSettings);
                else return CreateGenericEnumerableTypeReaderViaStrategy<T, E, StringReader_WithoutStringCache_Strategy, string>(elementTypeReader, constructor, bufferPool, typeSettings);
            }
            if (typeof(E) == typeof(char)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, CharReaderStrategy, char>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(sbyte)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, SByteReaderStrategy, sbyte>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(byte)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, ByteReaderStrategy, byte>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(short)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, Int16ReaderStrategy, short>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(ushort)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, UInt16ReaderStrategy, ushort>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(int)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, Int32ReaderStrategy, int>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(uint)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, UInt32ReaderStrategy, uint>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(long)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, Int64ReaderStrategy, long>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(ulong)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, UInt64ReaderStrategy, ulong>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(bool)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, BoolReaderStrategy, bool>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(float)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, FloatReaderStrategy, float>(elementTypeReader, constructor, bufferPool, typeSettings);
            if (typeof(E) == typeof(double)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, DoubleReaderStrategy, double>(elementTypeReader, constructor, bufferPool, typeSettings);
            return CreateGenericEnumerableTypeReaderViaStrategy<T, E, GenericReaderStrategy<E>, E>(elementTypeReader, constructor, bufferPool, typeSettings);
        }
        else
        {
            var elementReaderPool = new Pool<ElementReader<E>>(() => new ElementReader<E>(this), l => l.Reset(), 10, false);
            var reader = () =>
            {
                if (TryReadNullValue()) return default;
                var b = buffer.CurrentByte;
                if (b != '[') throw new Exception("Failed reading Array");
                if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
                var elementReader = elementReaderPool.Take();
                List<E> elementBuffer = bufferPool.Take();
                elementBuffer.AddRange(elementReader);
                T item = constructor(elementBuffer);
                bufferPool.Return(elementBuffer);
                if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
                buffer.TryNextByte();
                return item;
            };

            return TypeReaderInitializer.Create(this, reader, null, elementTypeReader.WriteRefPath, typeSettings);
        }
    }

    private TypeReaderInitializer CreateEnumerableTypeReader<T>(BaseTypeSettings typeSettings)
    {
        if (typeof(T).IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, true, typeSettings);
        }

        Func<IEnumerable, T> constructor = GetConstructor<T, IEnumerable>(typeSettings);
        Pool<List<object>> pool = new Pool<List<object>>(() => new List<object>(), l => l.Clear(), 1000, false);
        Pool<ElementReader<object>> elementReaderPool = new Pool<ElementReader<object>>(() => new ElementReader<object>(this), l => l.Reset(), 1000, false);
        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            var b = buffer.CurrentByte;
            if (b != '[') throw new Exception("Failed reading Array");
            if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
            ElementReader<object> elementReader = elementReaderPool.Take();
            List<object> elementBuffer = pool.Take();
            elementBuffer.AddRange(elementReader);
            T item = constructor(elementBuffer);
            pool.Return(elementBuffer);
            if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
            buffer.TryNextByte();
            return item;
        };

        return TypeReaderInitializer.Create(this, reader, null, true, typeSettings);
    }


    readonly List<ByteSegment> arrayElementNameCache = new List<ByteSegment>();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ByteSegment GetArrayElementName(int index)
    {
        while (arrayElementNameCache.Count <= index)
        {
            arrayElementNameCache.Add(new ByteSegment($"[{index}]".ToByteArray(), true));
        }
        return arrayElementNameCache[index];
    }

    private sealed class ElementReader<T> : IEnumerable<T>, IEnumerator<T>, IEnumerable, IEnumerator
    {
        JsonDeserializer deserializer;
        CachedTypeReader reader;
        T current = default;
        int index = -1;
        readonly bool writeRefPath;

        public ElementReader(JsonDeserializer deserializer)
        {
            this.deserializer = deserializer;            
            reader = deserializer.GetCachedTypeReader(typeof(T));
            writeRefPath =reader.WriteRefPath;
        }

        public ElementReader(JsonDeserializer deserializer, CachedTypeReader reader)
        {
            this.deserializer = deserializer;
            writeRefPath = reader.WriteRefPath;
            this.reader = reader;
        }

        public T Current => current;

        object IEnumerator.Current => current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            byte b = deserializer.SkipWhiteSpaces();
            if (b == ']')
            {
                return false;
            }
            if (writeRefPath) current = reader.ReadFieldValue<T>(deserializer.GetArrayElementName(++index));
            else current = reader.ReadValue_CheckProposed<T>();
            b = deserializer.SkipWhiteSpaces();
            if (b == ',') deserializer.buffer.TryNextByte();
            else if (deserializer.buffer.CurrentByte != ']')
            {
                throw new Exception("Failed reading Array");
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            index = -1;
            current = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<T> GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => this;
    }

    private sealed class NoCheck_ElementReader<T> : IEnumerable<T>, IEnumerator<T>, IEnumerable, IEnumerator
    {
        JsonDeserializer deserializer;
        CachedTypeReader reader;
        T current = default;

        public NoCheck_ElementReader(JsonDeserializer deserializer, CachedTypeReader reader)
        {
            this.deserializer = deserializer;
            this.reader = reader;
        }

        public T Current => current;

        object IEnumerator.Current => current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            byte b = deserializer.SkipWhiteSpaces();
            if (b == ']')
            {
                Reset();
                return false;
            }
            current = reader.ReadValue_NoCheck<T>();
            b = deserializer.SkipWhiteSpaces();
            if (b == ',') deserializer.buffer.TryNextByte();
            else if (deserializer.buffer.CurrentByte != ']')
            {
                throw new Exception("Failed reading Array");
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            current = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<T> GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => this;
    }

    private static bool CanTypeBePopulated(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return false;

        type = Nullable.GetUnderlyingType(type);
        if (type == null) return true;
        if (type.IsPrimitive || type.IsEnum) return false;

        return true;
    }


    private Type GetFieldOrPropertyType(MemberInfo fieldOrPropertyInfo)
    {
        if (fieldOrPropertyInfo is FieldInfo fieldInfo) return fieldInfo.FieldType;
        else if (fieldOrPropertyInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType;
        throw new Exception("Not a FieldType or PropertyType");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetItemRefInCurrentItemInfo(object item)
    {
#if NET5_0_OR_GREATER
        ref ItemInfo itemInfo = ref CollectionsMarshal.AsSpan(itemInfos)[currentItemInfoIndex];
        itemInfo.itemRef = item;
#else
        var itemInfo = itemInfos[currentItemInfoIndex];
        itemInfo.itemRef = item;
        itemInfos[currentItemInfoIndex] = itemInfo;
#endif
    }

    private Func<T> GetConstructor<T>(Type derivedType, BaseTypeSettings typeSettings)
    {
        Func<T> constructor = null;
        Type type = derivedType ?? typeof(T);
        if (typeSettings?.constructor != null && typeSettings.constructor is Func<T> castedConstructor) constructor = castedConstructor;
        else if (settings.constructors.TryGetValue(type, out object c) && c is Func<T> typedConstructor) constructor = () => typedConstructor();
        else if (type.IsValueType) return () => default;
        else if (!TryCompileConstructor<T>(out constructor, derivedType))
        {
            bool canUseUninitialized = settings.allowUninitializedObjectCreation &&
                                       settings.dataAccess == DataAccess.PublicAndPrivateFields;

            if (canUseUninitialized)
            {
                constructor = () => CreateUninitialized<T>();
            }
            else
            {
                throw new Exception(
                    $"No default constructor for type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)}. " +
                    $"Either add a custom constructor in settings, or enable uninitialized creation with {nameof(DataAccess.PublicAndPrivateFields)}.");
            }
        }

        if (!type.IsValueType && typeSettings?.enableReferenceResolution != false &&
            (settings.referenceResolutionMode == Settings.ReferenceResolutionMode.EnabledByDefault || 
            settings.referenceResolutionMode == Settings.ReferenceResolutionMode.EnabledByDefaultPlusStrings ||
            (settings.referenceResolutionMode == Settings.ReferenceResolutionMode.OnlyPerType && typeSettings?.enableReferenceResolution == true)))
        {
            return () =>
            {
                T item = constructor();
                SetItemRefInCurrentItemInfo(item);
                return item;
            };
        }
        else return constructor;
    }

    private Func<P, T> GetConstructor<T, P>(BaseTypeSettings typeSettings)
    {
        Func<P, T> constructor = null;
        Type type = typeof(T);
        if (typeSettings?.collectionConstructor != null && typeSettings.collectionConstructor is Func<P, T> castedConstructor) constructor = castedConstructor;
        else if (settings.constructorsWithParam.TryGetValue((type, typeof(P)), out object c) && c is Func<P, T> typedConstructor) constructor = typedConstructor;
        else if (!TryCompileConstructor(out constructor))
        {
            throw new Exception($"No constructor for type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)} with parameter {TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(P))}. Use AddConstructorWithParameter in Settings.");
        }

        if (!type.IsValueType && typeSettings?.enableReferenceResolution != false &&
            (settings.referenceResolutionMode == Settings.ReferenceResolutionMode.EnabledByDefault ||
            settings.referenceResolutionMode == Settings.ReferenceResolutionMode.EnabledByDefaultPlusStrings ||
            (settings.referenceResolutionMode == Settings.ReferenceResolutionMode.OnlyPerType && typeSettings?.enableReferenceResolution == true)))
        {
            return (parameter) =>
            {
                T item = constructor(parameter);
                SetItemRefInCurrentItemInfo(item);
                return item;
            };
        }
        else return constructor;
    }

    static T CreateUninitialized<T>()
    {
#if NET5_0_OR_GREATER
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
#else
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
#endif
    }

    private static bool TryCompileConstructor<T>(out Func<T> constructor, Type derivedType = null)
    {
        if (derivedType == null)
        {
            derivedType = typeof(T);
        }
        else
        {
            // Ensure the derivedType is actually a subclass of T
            if (!typeof(T).IsAssignableFrom(derivedType)) throw new ArgumentException($"{derivedType.Name} is not a subclass of {typeof(T).Name}");
        }

        constructor = null; // Ensure the out parameter is initialized

        var type = derivedType;

        // Try to get the parameterless constructor, including non-public ones
        var constructorInfo = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        if (constructorInfo == null)
        {
            return false; // No parameterless constructor found
        }

        // Create a new expression for the constructor
        var newExpression = Expression.New(constructorInfo);

        // Compile the lambda into a delegate
        constructor = Expression.Lambda<Func<T>>(newExpression, Array.Empty<ParameterExpression>()).Compile();

        return true;
    }

    private static bool TryCompileConstructor<T, P>(out Func<P, T> constructor, Type derivedType = null)
    {
        if (derivedType == null)
        {
            derivedType = typeof(T);
        }
        else
        {
            // Ensure the derivedType is actually a subclass of T
            if (!typeof(T).IsAssignableFrom(derivedType)) throw new ArgumentException($"{derivedType.Name} is not a subclass of {typeof(T).Name}");
        }

        constructor = null; // Ensure the out parameter is initialized

        var type = derivedType;
        var paramType = typeof(P);

        // Try to get the constructor that takes a single parameter of type P, including non-public ones
        var constructorInfo = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(P) },
            null);

        if (constructorInfo == null)
        {
            return false; // No matching constructor found
        }

        // Create a parameter expression for the input parameter
        var parameter = Expression.Parameter(typeof(P), "param");

        // Create a new expression for the constructor
        var newExpression = Expression.New(constructorInfo, parameter);

        // Compile the lambda into a delegate
        constructor = Expression.Lambda<Func<P, T>>(newExpression, parameter).Compile();

        return true;
    }
}

