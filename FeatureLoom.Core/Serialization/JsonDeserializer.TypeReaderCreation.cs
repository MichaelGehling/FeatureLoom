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
using static FeatureLoom.Serialization.JsonSerializer;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    Dictionary<Type, CachedTypeReader> typeReaderCache = new();
    Dictionary<ByteSegment, CachedTypeReader> proposedTypeReaderCache = new();
    readonly Dictionary<Type, bool> forbiddenTypeCache = new();

    CachedTypeReader CreateCachedTypeReader(Type itemType)
    {
        if (IsForbiddenType(itemType))
        {
            throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(itemType)} is forbidden for deserialization.");
        }
        if (settings.typeWhitelistMode == Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes && !IsWhitelistedType(itemType))
        {
            throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(itemType)} is not whitelisted for deserialization.");
        }

        if (settings.typeMapping.TryGetValue(itemType, out Type mappedType))
        {
            CachedTypeReader mappedTypeReader = CreateCachedTypeReader(mappedType);
            typeReaderCache[itemType] = mappedTypeReader;
            return mappedTypeReader;
        }

        if (itemType.IsGenericType && settings.genericTypeMapping.Count > 0)
        {
            Type genericType = itemType.GetGenericTypeDefinition();
            if (settings.genericTypeMapping.TryGetValue(genericType, out Type genericMappedType))
            {
                itemType = genericMappedType.MakeGenericType(itemType.GenericTypeArguments);
            }
        }

        return new CachedTypeReader((cachedTypeReader) =>
        {
            typeReaderCache[itemType] = cachedTypeReader;

            if (settings.customTypeReaders.TryGetValue(itemType, out object customReaderObj))
            {
                return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateCustomTypeReader), itemType.ToSingleEntryArray(), customReaderObj);
            }

            if (itemType.IsArray) return CreateArrayTypeReader(itemType, cachedTypeReader);
            else if (itemType == typeof(string)) return TypeReaderInitializer.Create(this, ReadStringValueOrNull, null, settings.enableStringRefResolution);
            else if (itemType == typeof(long)) return TypeReaderInitializer.Create(this, ReadLongValue, null, false);
            else if (itemType == typeof(long?)) return TypeReaderInitializer.Create(this, ReadNullableLongValue, null, false);
            else if (itemType == typeof(int)) return TypeReaderInitializer.Create(this, ReadIntValue, null, false);
            else if (itemType == typeof(int?)) return TypeReaderInitializer.Create(this, ReadNullableIntValue, null, false);
            else if (itemType == typeof(short)) return TypeReaderInitializer.Create(this, ReadShortValue, null, false);
            else if (itemType == typeof(short?)) return TypeReaderInitializer.Create(this, ReadNullableShortValue, null, false);
            else if (itemType == typeof(sbyte)) return TypeReaderInitializer.Create(this, ReadSbyteValue, null, false);
            else if (itemType == typeof(sbyte?)) return TypeReaderInitializer.Create(this, ReadNullableSbyteValue, null, false);
            else if (itemType == typeof(ulong)) return TypeReaderInitializer.Create(this, ReadUlongValue, null, false);
            else if (itemType == typeof(ulong?)) return TypeReaderInitializer.Create(this, ReadNullableUlongValue, null, false);
            else if (itemType == typeof(uint)) return TypeReaderInitializer.Create(this, ReadUintValue, null, false);
            else if (itemType == typeof(uint?)) return TypeReaderInitializer.Create(this, ReadNullableUintValue, null, false);
            else if (itemType == typeof(ushort)) return TypeReaderInitializer.Create(this, ReadUshortValue, null, false);
            else if (itemType == typeof(ushort?)) return TypeReaderInitializer.Create(this, ReadNullableUshortValue, null, false);
            else if (itemType == typeof(byte)) return TypeReaderInitializer.Create(this, ReadByteValue, null, false);
            else if (itemType == typeof(byte?)) return TypeReaderInitializer.Create(this, ReadNullableByteValue, null, false);
            else if (itemType == typeof(double)) return TypeReaderInitializer.Create(this, ReadDoubleValue, null, false);
            else if (itemType == typeof(double?)) return TypeReaderInitializer.Create(this, ReadNullableDoubleValue, null, false);
            else if (itemType == typeof(float)) return TypeReaderInitializer.Create(this, ReadFloatValue, null, false);
            else if (itemType == typeof(float?)) return TypeReaderInitializer.Create(this, ReadNullableFloatValue, null, false);
            else if (itemType == typeof(decimal)) return TypeReaderInitializer.Create(this, ReadDecimalValue, null, false);
            else if (itemType == typeof(decimal?)) return TypeReaderInitializer.Create(this, ReadNullableDecimalValue, null, false);
            else if (itemType == typeof(bool)) return TypeReaderInitializer.Create(this, ReadBoolValue, null, false);
            else if (itemType == typeof(bool?)) return TypeReaderInitializer.Create(this, ReadNullableBoolValue, null, false);
            else if (itemType == typeof(char)) return TypeReaderInitializer.Create(this, ReadCharValue, null, false);
            else if (itemType == typeof(char?)) return TypeReaderInitializer.Create(this, ReadNullableCharValue, null, false);
            else if (itemType == typeof(DateTime)) return TypeReaderInitializer.Create(this, ReadDateTimeValue, null, false);
            else if (itemType == typeof(DateTime?)) return TypeReaderInitializer.Create(this, ReadNullableDateTimeValue, null, false);
            else if (itemType == typeof(DateTimeOffset)) return TypeReaderInitializer.Create(this, ReadDateTimeOffsetValue, null, false);
            else if (itemType == typeof(DateTimeOffset?)) return TypeReaderInitializer.Create(this, ReadNullableDateTimeOffsetValue, null, false);
            else if (itemType == typeof(TimeSpan)) return TypeReaderInitializer.Create(this, ReadTimeSpanValue, null, false);
            else if (itemType == typeof(TimeSpan?)) return TypeReaderInitializer.Create(this, ReadNullableTimeSpanValue, null, false);
            else if (itemType == typeof(Guid)) return TypeReaderInitializer.Create(this, ReadGuidValue, null, false);
            else if (itemType == typeof(Guid?)) return TypeReaderInitializer.Create(this, ReadNullableGuidValue, null, false);
            else if (itemType == typeof(JsonFragment)) return TypeReaderInitializer.Create(this, ReadJsonFragmentValue, null, false);
            else if (itemType == typeof(JsonFragment?)) return TypeReaderInitializer.Create(this, ReadNullableJsonFragmentValue, null, false);
            else if (itemType == typeof(IntPtr)) return TypeReaderInitializer.Create(this, ReadIntPtrValue, null, false);
            else if (itemType == typeof(UIntPtr)) return TypeReaderInitializer.Create(this, ReadUIntPtrValue, null, false);
            else if (itemType == typeof(Uri)) return TypeReaderInitializer.Create(this, () => { var s = ReadStringValueOrNull(); return s == null ? null : new Uri(s); }, null, true);
            else if (itemType == typeof(ByteSegment)) return CreateByteSegmentTypeReader();
            else if (itemType == typeof(ByteSegment?)) return CreateNullableByteSegmentTypeReader();
            else if (itemType == typeof(ArraySegment<byte>)) return CreateByteArraySegmentTypeReader();
            else if (itemType == typeof(ArraySegment<byte>?)) return CreateNullableByteArraySegmentTypeReader();
            else if (itemType == typeof(TextSegment)) return CreateTextSegmentTypeReader();
            else if (itemType.IsEnum || (Nullable.GetUnderlyingType(itemType)?.IsEnum ?? false))
            {
                if (!itemType.IsNullable()) return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateEnumReader), new Type[] { itemType });
                else return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateNullableEnumReader), new Type[] { Nullable.GetUnderlyingType(itemType) });
            }
            else if (itemType == typeof(object)) return CreateUnknownObjectReader(cachedTypeReader);
            else if (TryCreateDictionaryTypeReader(itemType, out TypeReaderInitializer initializer)) return initializer;
            else if (TryCreateEnumerableTypeReader(itemType, out initializer)) return initializer;
            else return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { itemType }, true);
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

    TypeReaderInitializer CreateCustomTypeReader<T>(object customReaderObj)
    {
        var customReader = (ICustomTypeReader<T>)customReaderObj;
        var reader = () =>
        {
            return customReader.ReadValue(this.extensionApi);
        };
        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateByteArrayTypeReader()
    {
        var byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }));
        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            return ReadByteArray(byteArrayReader);
        };

        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateByteSegmentTypeReader()
    {
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ByteSegment) }, false));

        var reader = () =>
        {
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ByteSegment>();
            return new ByteSegment(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateNullableByteSegmentTypeReader()
    {
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ByteSegment) }, false));

        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ByteSegment>();
            return (ByteSegment?)new ByteSegment(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateByteArraySegmentTypeReader()
    {
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ArraySegment<byte>) }, false));

        var reader = () =>
        {
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ArraySegment<byte>>();
            return new ArraySegment<byte>(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateNullableByteArraySegmentTypeReader()
    {
        CachedTypeReader byteArrayReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }));
        CachedTypeReader objectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(ArraySegment<byte>) }, false));

        var reader = () =>
        {
            if (TryReadNullValue()) return default;
            if (buffer.CurrentByte == '{') return objectReader.ReadValue_CheckProposed<ArraySegment<byte>>();
            return (ArraySegment<byte>?)new ArraySegment<byte>(ReadByteArray(byteArrayReader));
        };

        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateTextSegmentTypeReader()
    {
        CachedTypeReader textSegmentObjectReader = new CachedTypeReader((_) => this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateComplexTypeReader), new Type[] { typeof(TextSegment) }, false));

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

        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateEnumReader<T>() where T : struct, Enum
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

        return TypeReaderInitializer.Create(this, reader, null, false);
    }

    private TypeReaderInitializer CreateNullableEnumReader<T>() where T : struct, Enum
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

        return TypeReaderInitializer.Create(this, reader, null, false);
    }

    private bool TryCreateDictionaryTypeReader(Type itemType, out TypeReaderInitializer initializer)
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

        initializer = this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateDictionaryTypeReader), [itemType, keyType, valueType]);
        return true;
    }

    private TypeReaderInitializer CreateDictionaryTypeReader<T, K, V>() where T : IDictionary<K, V>, new()
    {
        if (typeof(T).IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping or multi option type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, true);
        }

        var constructor = GetConstructor<T>();
        var elementReader = new ElementReader<KeyValuePair<K, V>>(this);
        var keyReader = GetCachedTypeReader(typeof(K));
        var valueReader = GetCachedTypeReader(typeof(V));
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

        return TypeReaderInitializer.Create(this, reader, populatingReader, true);
    }

    private TypeReaderInitializer CreateUnknownObjectReader(CachedTypeReader cachedTypeReader)
    {
        if (!settings.multiOptionTypeMapping.TryGetValue(typeof(object), out var typeOptions))
        {
            return TypeReaderInitializer.Create(this, ReadUnknownValue, null, true);
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
            return TypeReaderInitializer.Create(this, ReadUnknownValue, null, true);
        }

        if (arrayTypeOption == null) arrayTypeOption = typeof(List<object>);
        objectTypeOptions.Add(typeof(Dictionary<string, object>));

        var arrayReader = GetCachedTypeReader(arrayTypeOption);
        CachedTypeReader objectReader = new CachedTypeReader((_) => CreateMultiOptionComplexTypeReader<object>(objectTypeOptions.ToArray()));

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

        return TypeReaderInitializer.Create(this, reader, null, true);

    }

    private TypeReaderInitializer CreateMultiOptionComplexTypeReader<T>(Type[] typeOptions)
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
                    return new CachedTypeReader((_) => CreateComplexTypeReader<T>(false));
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
            var memberInfos = CreateMemberInfosList(typeOption);

            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                string name = memberInfo.Name;
                if (name.TryExtract("<{name}>k__BackingField", out string backingFieldName)) name = backingFieldName;
                var itemFieldName = new ByteSegment(name.ToByteArray(), true);
                if (!fieldNameToIsTypeMember.TryGetValue(itemFieldName, out var indicesList))
                {
                    indicesList = Enumerable.Repeat(false, objectTypeOptions.Length).ToList();
                    fieldNameToIsTypeMember[itemFieldName] = indicesList;
                }
                indicesList[i] = true;
                // TODO: BackingFields are not yet supported for multi options
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

        return TypeReaderInitializer.Create(this, reader, null, true);
    }

    private TypeReaderInitializer CreateComplexTypeReader<T>(bool checkForMultiOptions)
    {
        Type itemType = typeof(T);

        if (checkForMultiOptions && settings.multiOptionTypeMapping.TryGetValue(itemType, out Type[] mappedTypeOptions))
        {
            return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateMultiOptionComplexTypeReader), [itemType], [mappedTypeOptions]);
        }

        if (itemType.IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping or multi option type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, true);
        }
        List<MemberInfo> memberInfos = CreateMemberInfosList(itemType);

        Dictionary<ByteSegment, int> itemFieldWritersIndexLookup = new();
        List<(ByteSegment name, Func<T, T> itemFieldWriter)> itemFieldWritersList = new();
        bool refTypeOrRefTypeChildren = !itemType.IsValueType;
        foreach (var memberInfo in memberInfos)
        {
            Type fieldType = GetFieldOrPropertyType(memberInfo);
            string name = memberInfo.Name;
            var itemFieldName = new ByteSegment(name.ToByteArray(), true);
            var itemFieldWriter = this.InvokeGenericMethod<Func<T, T>>(nameof(CreateItemFieldWriter), new Type[] { itemType, fieldType, itemType }, memberInfo, itemFieldName);
            itemFieldWritersIndexLookup[itemFieldName] = itemFieldWritersList.Count;
            itemFieldWritersList.Add((itemFieldName, itemFieldWriter));

            if (name.TryExtract("<{name}>k__BackingField", out string backingFieldName))
            {
                name = backingFieldName;
                itemFieldName = new ByteSegment(name.ToByteArray(), true);
                itemFieldWritersIndexLookup[itemFieldName] = itemFieldWritersList.Count;
                itemFieldWritersList.Add((itemFieldName, itemFieldWriter));
            }

            if (!refTypeOrRefTypeChildren && GetCachedTypeReader(fieldType).RefTypeOrRefTypeChildren) refTypeOrRefTypeChildren = true;
        }
        int writerCount = itemFieldWritersList.Count;
        var itemFieldWriters = itemFieldWritersList.ToArray();
        itemFieldWritersList = null;
        var constructor = GetConstructor<T>();

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

        return TypeReaderInitializer.Create(this, reader, populatingReader, refTypeOrRefTypeChildren);
    }

    private List<MemberInfo> CreateMemberInfosList(Type itemType)
    {
        var memberInfos = new List<MemberInfo>();
        if (settings.dataAccess == DataAccess.PublicFieldsAndProperties)
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

    private Func<C, C> CreateItemFieldWriter<T, V, C>(MemberInfo memberInfo, ByteSegment fieldName) where T : C
    {
        Type itemType = typeof(T);
        Type fieldType = typeof(V);

        if (memberInfo is FieldInfo fieldInfo)
        {
            if (fieldInfo.IsInitOnly) // Check if the field is read-only
            {
                // Handle read-only fields
                return CreateFieldWriterForInitOnlyFields<T, V, C>(fieldName, itemType, fieldType, fieldInfo);
            }
            else
            {
                // Use expression tree for normal writable field
                return CreateFieldWriterUsingExpression<T, V, C>(fieldInfo, fieldName);
            }
        }
        else if (memberInfo is PropertyInfo propertyInfo)
        {
            if (propertyInfo.CanWrite)
            {
                // Includes init-only properties in current runtime behavior:
                // init accessors are reported as writable here.
                // Use expression tree for writable property
                return CreatePropertyWriterUsingExpression<T, V, C>(propertyInfo, fieldName);
            }
            // Intentionally disabled:
            // In this codebase/runtime context, init-only properties are already covered by CanWrite == true,
            // so this fallback branch is not expected to be reachable.
            //
            // else if (HasInitAccessor(propertyInfo))
            // {
            //     return CreateFieldWriterForInitOnlyProperties<T, V, C>(fieldName, itemType, fieldType, propertyInfo);
            // }
        }

        throw new InvalidOperationException("MemberInfo must be a writable field, property, or init-only property.");
    }

    /*
    private Func<C, C> CreateFieldWriterForInitOnlyProperties<T, V, C>(ByteSegment fieldName, Type itemType, Type fieldType, PropertyInfo propertyInfo) where T : C
    {            
        var fieldTypeReader = GetCachedTypeReader(fieldType);
        if (itemType.IsValueType)
        {
            if (fieldTypeReader.CanBePopulated && propertyInfo.CanRead)
            {
                return parentItem =>
                {
                    V value = (V)propertyInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                    value = fieldTypeReader.ReadFieldValue<V>(fieldName, value);
                    var boxedItem = (object)parentItem;
                    propertyInfo.SetValue(boxedItem, value);
                    parentItem = (T)boxedItem;
                    return parentItem;
                };
            }
            else
            {
                return parentItem =>
                {
                    V value = fieldTypeReader.ReadFieldValue<V>(fieldName);
                    var boxedItem = (object)parentItem;
                    propertyInfo.SetValue(boxedItem, value);
                    parentItem = (T)boxedItem;
                    return parentItem;
                };
            }
        }
        else
        {
            if (fieldTypeReader.CanBePopulated && propertyInfo.CanRead)
            {
                return parentItem =>
                {
                    V value = (V)propertyInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                    value = fieldTypeReader.ReadFieldValue<V>(fieldName, value);
                    propertyInfo.SetValue(parentItem, value);            
                    return parentItem;
                };
            }
            else
            {
                return parentItem =>
                {
                    V value = fieldTypeReader.ReadFieldValue<V>(fieldName);
                    propertyInfo.SetValue(parentItem, value);
                    return parentItem;
                };
            }
        }
    }
    */

    private Func<C, C> CreateFieldWriterForInitOnlyFields<T, V, C>(ByteSegment fieldName, Type itemType, Type fieldType, FieldInfo fieldInfo) where T : C
    {
        var fieldTypeReader = GetCachedTypeReader(fieldType);
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
                        return parentItem =>
                        {
                            var v = ReadStringValue();
                            V value = Unsafe.As<string, V>(ref v);
                            var boxedItem = (object)parentItem;
                            fieldInfo.SetValue(boxedItem, value);
                            parentItem = (T)boxedItem;
                            return parentItem;
                        };
                    }
                    if (typeof(V) == typeof(int))
                    {
                        return parentItem =>
                        {
                            var v = ReadIntValue();
                            V value = Unsafe.As<int, V>(ref v);
                            var boxedItem = (object)parentItem;
                            fieldInfo.SetValue(boxedItem, value);
                            parentItem = (T)boxedItem;
                            return parentItem;
                        };
                    }

                    return parentItem =>
                    {
                        V value = fieldTypeReader.ReadValue_NoCheck<V>();
                        var boxedItem = (object)parentItem;
                        fieldInfo.SetValue(boxedItem, value);
                        parentItem = (T)boxedItem;
                        return parentItem;
                    };
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
                        return parentItem =>
                        {
                            var v = ReadStringValue();
                            V value = Unsafe.As<string, V>(ref v);
                            fieldInfo.SetValue(parentItem, value);
                            return parentItem;
                        };
                    }
                    if (typeof(V) == typeof(int))
                    {
                        return parentItem =>
                        {
                            var v = ReadIntValue();
                            V value = Unsafe.As<int, V>(ref v);
                            fieldInfo.SetValue(parentItem, value);
                            return parentItem;
                        };
                    }

                    return parentItem =>
                    {
                        V value = fieldTypeReader.ReadValue_NoCheck<V>();
                        fieldInfo.SetValue(parentItem, value);
                        return parentItem;
                    };
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
#if NET5_0_OR_GREATER
    private bool HasInitAccessor(PropertyInfo propertyInfo)
    {
        // Use reflection to check if the property has an init accessor
        var setMethod = propertyInfo.SetMethod;
        return setMethod != null && setMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
    }
#else
    private bool HasInitAccessor(PropertyInfo propertyInfo)
    {
        // .NET Standard 2.0 + 2.1 doesn't support init-only properties, so return false
        return false;
    }
#endif

    private Func<C, C> CreateFieldWriterUsingExpression<T, V, C>(FieldInfo fieldInfo, ByteSegment fieldName) where T : C
    {
        Type itemType = typeof(T);
        Type fieldType = typeof(V);

        var fieldTypeReader = GetCachedTypeReader(fieldType);

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
                    if (typeof(V) == typeof(string)) return CreateValueFieldWriterViaStrategy<T, V, C, StringReaderStrategy, string>(setValueAndReturn, fieldTypeReader);
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
#if NET5_0_OR_GREATER
                    if (typeof(V) == typeof(string)) return CreateObjFieldWriterViaStrategy<T, V, C, StringReaderStrategy, string>(setValue, fieldTypeReader);
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
#else

                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadValue_NoCheck<V>();
                        setValue((T)parentItem, fieldValue);
                        return parentItem;
                    };
#endif
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

    private Func<C, C> CreatePropertyWriterUsingExpression<T, V, C>(PropertyInfo propertyInfo, ByteSegment fieldName) where T : C
    {
        Type itemType = typeof(T);
        Type fieldType = typeof(V);

        var fieldTypeReader = GetCachedTypeReader(fieldType);

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

                    if (typeof(V) == typeof(string)) return CreateValueFieldWriterViaStrategy<T, V, C, StringReaderStrategy, string>(setValueAndReturn, fieldTypeReader);
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
#if NET5_0_OR_GREATER
                    if (typeof(V) == typeof(string)) return CreateObjFieldWriterViaStrategy<T, V, C, StringReaderStrategy, string>(setValue, fieldTypeReader);
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
#else

                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadValue_NoCheck<V>();
                        setValue((T)parentItem, fieldValue);
                        return parentItem;
                    };
#endif
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

    private TypeReaderInitializer CreateArrayTypeReader(Type arrayType, CachedTypeReader cachedTypeReader)
    {
        if (arrayType == typeof(byte[]))
        {
            return CreateByteArrayTypeReader();
        }
        else
        {
            return this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericArrayTypeReader), new Type[] { arrayType.GetElementType() });
        }
    }

    private TypeReaderInitializer CreateGenericArrayTypeReader<E>()
    {
        var pool = new Pool<List<E>>(() => new List<E>(), l => l.Clear(), 10, false);

        var elementTypeReader = GetCachedTypeReader(typeof(E));
        if (elementTypeReader.IsNoCheckPossible<E>())
        {
            if (typeof(E) == typeof(string)) return CreateGenericArrayTypeReaderViaStrategy<E, StringReaderStrategy, string>(elementTypeReader, pool);
            if (typeof(E) == typeof(char)) return CreateGenericArrayTypeReaderViaStrategy<E, CharReaderStrategy, char>(elementTypeReader, pool);
            if (typeof(E) == typeof(sbyte)) return CreateGenericArrayTypeReaderViaStrategy<E, SByteReaderStrategy, sbyte>(elementTypeReader, pool);
            if (typeof(E) == typeof(byte)) return CreateGenericArrayTypeReaderViaStrategy<E, ByteReaderStrategy, byte>(elementTypeReader, pool);
            if (typeof(E) == typeof(short)) return CreateGenericArrayTypeReaderViaStrategy<E, Int16ReaderStrategy, short>(elementTypeReader, pool);
            if (typeof(E) == typeof(ushort)) return CreateGenericArrayTypeReaderViaStrategy<E, UInt16ReaderStrategy, ushort>(elementTypeReader, pool);
            if (typeof(E) == typeof(int)) return CreateGenericArrayTypeReaderViaStrategy<E, Int32ReaderStrategy, int>(elementTypeReader, pool);
            if (typeof(E) == typeof(uint)) return CreateGenericArrayTypeReaderViaStrategy<E, UInt32ReaderStrategy, uint>(elementTypeReader, pool);
            if (typeof(E) == typeof(long)) return CreateGenericArrayTypeReaderViaStrategy<E, Int64ReaderStrategy, long>(elementTypeReader, pool);
            if (typeof(E) == typeof(ulong)) return CreateGenericArrayTypeReaderViaStrategy<E, UInt64ReaderStrategy, ulong>(elementTypeReader, pool);
            if (typeof(E) == typeof(bool)) return CreateGenericArrayTypeReaderViaStrategy<E, BoolReaderStrategy, bool>(elementTypeReader, pool);
            if (typeof(E) == typeof(float)) return CreateGenericArrayTypeReaderViaStrategy<E, FloatReaderStrategy, float>(elementTypeReader, pool);
            if (typeof(E) == typeof(double)) return CreateGenericArrayTypeReaderViaStrategy<E, DoubleReaderStrategy, double>(elementTypeReader, pool);
            return CreateGenericArrayTypeReaderViaStrategy<E, GenericReaderStrategy<E>, E>(elementTypeReader, pool);
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
                if (settings.enableReferenceResolution) SetItemRefInCurrentItemInfo(item);
                pool.Return(elementBuffer);
                elementReaderPool.Return(elementReader);
                if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
                buffer.TryNextByte();
                return item;
            };

            return TypeReaderInitializer.Create(this, reader, null, true);
        }
    }

    private bool TryCreateEnumerableTypeReader(Type itemType, out TypeReaderInitializer initializer)
    {
        if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType))
        {
            initializer = this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateGenericEnumerableTypeReader), new Type[] { itemType, elementType });
        }
        else if (itemType.ImplementsInterface(typeof(IEnumerable)))
        {
            initializer = this.InvokeGenericMethod<TypeReaderInitializer>(nameof(CreateEnumerableTypeReader), new Type[] { itemType });
        }
        else
        {
            initializer = null;
            return false;
        }

        return true;
    }



    private TypeReaderInitializer CreateGenericEnumerableTypeReader<T, E>()
    {
        if (typeof(T).IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, true);
        }

        Func<IEnumerable<E>, T> constructor = GetConstructor<T, IEnumerable<E>>();
        var elementTypeReader = GetCachedTypeReader(typeof(E));
        Pool<List<E>> bufferPool = new Pool<List<E>>(() => new List<E>(), l => l.Clear(), 10, false);

        if (elementTypeReader.IsNoCheckPossible<E>())
        {
            if (typeof(E) == typeof(string)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, StringReaderStrategy, string>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(char)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, CharReaderStrategy, char>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(sbyte)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, SByteReaderStrategy, sbyte>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(byte)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, ByteReaderStrategy, byte>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(short)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, Int16ReaderStrategy, short>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(ushort)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, UInt16ReaderStrategy, ushort>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(int)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, Int32ReaderStrategy, int>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(uint)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, UInt32ReaderStrategy, uint>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(long)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, Int64ReaderStrategy, long>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(ulong)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, UInt64ReaderStrategy, ulong>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(bool)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, BoolReaderStrategy, bool>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(float)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, FloatReaderStrategy, float>(elementTypeReader, constructor, bufferPool);
            if (typeof(E) == typeof(double)) return CreateGenericEnumerableTypeReaderViaStrategy<T, E, DoubleReaderStrategy, double>(elementTypeReader, constructor, bufferPool);
            return CreateGenericEnumerableTypeReaderViaStrategy<T, E, GenericReaderStrategy<E>, E>(elementTypeReader, constructor, bufferPool);
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

            return TypeReaderInitializer.Create(this, reader, null, true);
        }
    }

    private TypeReaderInitializer CreateEnumerableTypeReader<T>()
    {
        if (typeof(T).IsAbstract)
        {
            // For abstract types we cannot create a type reader, but we want to create a placeholder type reader that throws an exception when trying to read a value.
            // If the user wants to deserialize to an abstract type, they either have to provide a proposed type with the $type property or
            // use the type mapping in the settings.
            return TypeReaderInitializer.Create<T>(this, null, null, true);
        }

        Func<IEnumerable, T> constructor = GetConstructor<T, IEnumerable>();
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

        return TypeReaderInitializer.Create(this, reader, null, true);
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
        readonly bool enableReferenceResolution;

        public ElementReader(JsonDeserializer deserializer)
        {
            this.deserializer = deserializer;
            enableReferenceResolution = deserializer.settings.enableReferenceResolution;
            reader = deserializer.GetCachedTypeReader(typeof(T));
        }

        public ElementReader(JsonDeserializer deserializer, CachedTypeReader reader)
        {
            this.deserializer = deserializer;
            enableReferenceResolution = deserializer.settings.enableReferenceResolution;
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
            if (enableReferenceResolution) current = reader.ReadFieldValue<T>(deserializer.GetArrayElementName(++index));
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
        readonly bool enableReferenceResolution;

        public NoCheck_ElementReader(JsonDeserializer deserializer, CachedTypeReader reader)
        {
            this.deserializer = deserializer;
            enableReferenceResolution = deserializer.settings.enableReferenceResolution;
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

    private Func<T> GetConstructor<T>(Type derivedType = null)
    {
        Func<T> constructor = null;
        Type type = derivedType ?? typeof(T);
        if (settings.constructors.TryGetValue(type, out object c) && c is Func<T> typedConstructor) constructor = () => typedConstructor();
        else if (type.IsValueType) return () => default;
        else if (!TryCompileConstructor<T>(out constructor, derivedType))
        {
            bool canUseUninitialized =
    settings.allowUninitializedObjectCreation &&
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

        if (!type.IsValueType && settings.enableReferenceResolution)
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

    private Func<P, T> GetConstructor<T, P>()
    {
        Func<P, T> constructor = null;
        Type type = typeof(T);
        if (settings.constructorsWithParam.TryGetValue((type, typeof(P)), out object c) && c is Func<P, T> typedConstructor) constructor = typedConstructor;
        else if (!TryCompileConstructor(out constructor))
        {
            throw new Exception($"No constructor for type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)} with parameter {TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(P))}. Use AddConstructorWithParameter in Settings.");
        }

        if (!type.IsValueType && settings.enableReferenceResolution)
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

