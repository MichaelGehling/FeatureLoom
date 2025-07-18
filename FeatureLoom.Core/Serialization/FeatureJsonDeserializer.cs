﻿using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Collections;
using System.Runtime.CompilerServices;
using FeatureLoom.Helpers;
using System.Linq.Expressions;
using FeatureLoom.Collections;
using static FeatureLoom.Serialization.FeatureJsonSerializer;
using System.Collections.Concurrent;
using System.Net;
using System.ComponentModel;
using System.Runtime.Serialization.Formatters;
using System.Net.Http.Headers;
using FeatureLoom.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FeatureLoom.Logging;

#if !NETSTANDARD2_0
using System.Buffers.Text;
using System.Buffers;
#endif

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonDeserializer
    {        
        readonly Buffer buffer = new Buffer();

        MicroValueLock serializerLock = new MicroValueLock();                        
        
        ByteSegment rootName = "$".ToByteArray();
        ByteSegment currentItemName = "$".ToByteArray();
        int currentItemInfoIndex = -1;
        List<ItemInfo> itemInfos = new List<ItemInfo>();
        bool isPopulating = false;

        ExtensionApi extensionApi;

        Dictionary<Type, CachedTypeReader> typeReaderCache = new();
        Dictionary<Type, object> typeConstructorMap = new();

        static readonly FilterResult[] map_SkipWhitespaces = CreateFilterMap_SkipWhitespaces();
        static readonly FilterResult[] map_IsFieldEnd = CreateFilterMap_IsFieldEnd();
        static readonly FilterResult[] map_SkipCharsUntilStringEndsOrMultiByteChar = CreateFilterMap_SkipCharsUntilStringEndsOrMultiByteChar();
        static readonly FilterResult[] map_SkipWhitespacesUntilNumberStarts = CreateFilterMap_SkipWhitespacesUntilNumberStarts();
        static readonly FilterResult[] map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds = CreateFilterMap_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds();
        static readonly FilterResult[] map_SkipFiguresUntilExponentOrNumberEnds = CreateFilterMap_SkipFiguresUntilExponentOrNumberEnds();
        static readonly FilterResult[] map_SkipFiguresUntilNumberEnds = CreateFilterMap_SkipFiguresUntilNumberEnds();
        static readonly FilterResult[] map_SkipWhitespacesUntilObjectStarts = CreateFilterMap_SkipWhitespacesUntilObjectStarts();
        static readonly TypeResult[] map_TypeStart = CreateTypeStartMap();

        static ulong[] exponentFactorMap = CreateExponentFactorMap(19);

        struct ItemInfo
        {
            public ByteSegment name;
            public int parentIndex;
            public object itemRef;

            public ItemInfo(ByteSegment name, int parentIndex)
            {
                this.name = name;
                this.parentIndex = parentIndex;
            }
        }

        static ulong[] CreateExponentFactorMap(int maxExponent)
        {
            ulong[] map = new ulong[maxExponent + 1];
            ulong factor = 1;
            map[0] = factor;
            for (int i = 1; i < map.Length; i++)
            {
                factor *= 10;
                map[i] = factor;
            }
            return map;
        }

        public enum DataAccess
        {
            PublicAndPrivateFields = 0,
            PublicFieldsAndProperties = 1
        }

        readonly Settings settings;


        public FeatureJsonDeserializer(Settings settings = null)
        {
            this.settings = settings ?? new Settings();            
            buffer.Init(this.settings.initialBufferSize);            
            extensionApi = new ExtensionApi(this);
            isPopulating = settings.populateExistingMembers;
        }

        private void Reset()
        {
            buffer.ResetAfterReading();
            isPopulating = settings.populateExistingMembers;

            if (settings.enableReferenceResolution)
            {
                currentItemName = rootName;                
                itemInfos.Clear();
                currentItemInfoIndex = -1;
            }
        }

        public string ShowBufferAroundCurrentPosition(int before = 100, int after = 50) => buffer.ShowBufferAroundCurrentPosition(before, after); 
        
        public void SkipBufferUntil(string delimiter, bool alsoSkipDelimiter, out bool found)
        {
            found = false;
            if (delimiter.EmptyOrNull()) return;

            serializerLock.Enter();
            try
            {                
                ByteSegment delimiterBytes = Encoding.UTF8.GetBytes(delimiter);

                if (buffer.CountRemainingBytes < delimiterBytes.Count)
                {
                    if (buffer.CountSizeLeft == 0) buffer.ResetBuffer(true, false);
                    buffer.TryReadFromStream();
                }
                do
                {
                    ByteSegment bufferBytes = buffer.GetRemainingBytes();
                    if (bufferBytes.TryFindIndex(delimiterBytes, out int index))
                    {
                        found = true;
                        buffer.TrySkipBytes(index + (alsoSkipDelimiter ? delimiterBytes.Count : 0));
                        buffer.ResetAfterReading();
                        return;
                    }
                    buffer.TrySkipBytes(bufferBytes.Count - delimiterBytes.Count); //Ensure to keep the last chars for the case that the delimiter was split
                    buffer.ResetBufferAfterFullSkip();
                }
                while (buffer.TryReadFromStream());
            }
            catch(Exception ex)
            {
                OptLog.ERROR()?.Build("Error occurred on skipping buffer.", ex);                
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        CachedTypeReader GetCachedTypeReader(Type itemType)
        {
            if (typeReaderCache.TryGetValue(itemType, out var cachedTypeReader)) return cachedTypeReader;
            else return CreateCachedTypeReader(itemType);
        }

        CachedTypeReader CreateCachedTypeReader(Type itemType)
        {
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

            CachedTypeReader cachedTypeReader = new CachedTypeReader(this);
            typeReaderCache[itemType] = cachedTypeReader;

            if (settings.customTypeReaders.TryGetValue(itemType, out object customReaderObj))
            {                
                cachedTypeReader.InvokeGenericMethod(nameof(CachedTypeReader.SetCustomTypeReader), itemType.ToSingleEntryArray(), customReaderObj);
                return cachedTypeReader;
            }
            
            if (itemType.IsArray) CreateArrayTypeReader(itemType, cachedTypeReader);
            else if (itemType == typeof(string)) cachedTypeReader.SetTypeReader(ReadStringValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(long)) cachedTypeReader.SetTypeReader(ReadLongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(long?)) cachedTypeReader.SetTypeReader(ReadNullableLongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(int)) cachedTypeReader.SetTypeReader(ReadIntValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(int?)) cachedTypeReader.SetTypeReader(ReadNullableIntValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(short)) cachedTypeReader.SetTypeReader(ReadShortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(short?)) cachedTypeReader.SetTypeReader(ReadNullableShortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(sbyte)) cachedTypeReader.SetTypeReader(ReadSbyteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(sbyte?)) cachedTypeReader.SetTypeReader(ReadNullableSbyteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ulong)) cachedTypeReader.SetTypeReader(ReadUlongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ulong?)) cachedTypeReader.SetTypeReader(ReadNullableUlongValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(uint)) cachedTypeReader.SetTypeReader(ReadUintValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(uint?)) cachedTypeReader.SetTypeReader(ReadNullableUintValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ushort)) cachedTypeReader.SetTypeReader(ReadUshortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(ushort?)) cachedTypeReader.SetTypeReader(ReadNullableUshortValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(byte)) cachedTypeReader.SetTypeReader(ReadByteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(byte?)) cachedTypeReader.SetTypeReader(ReadNullableByteValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(double)) cachedTypeReader.SetTypeReader(ReadDoubleValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(double?)) cachedTypeReader.SetTypeReader(ReadNullableDoubleValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(float)) cachedTypeReader.SetTypeReader(ReadFloatValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(float?)) cachedTypeReader.SetTypeReader(ReadNullableFloatValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(decimal)) cachedTypeReader.SetTypeReader(ReadDecimalValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(decimal?)) cachedTypeReader.SetTypeReader(ReadNullableDecimalValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(bool)) cachedTypeReader.SetTypeReader(ReadBoolValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(bool?)) cachedTypeReader.SetTypeReader(ReadNullableBoolValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(char)) cachedTypeReader.SetTypeReader(ReadCharValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(char?)) cachedTypeReader.SetTypeReader(ReadNullableCharValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(DateTime)) cachedTypeReader.SetTypeReader(ReadDateTimeValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(DateTime?)) cachedTypeReader.SetTypeReader(ReadNullableDateTimeValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(TimeSpan)) cachedTypeReader.SetTypeReader(ReadTimeSpanValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(TimeSpan?)) cachedTypeReader.SetTypeReader(ReadNullableTimeSpanValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(Guid)) cachedTypeReader.SetTypeReader(ReadGuidValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(Guid?)) cachedTypeReader.SetTypeReader(ReadNullableGuidValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(JsonFragment)) cachedTypeReader.SetTypeReader(ReadJsonFragmentValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(JsonFragment?)) cachedTypeReader.SetTypeReader(ReadNullableJsonFragmentValue, JsonDataTypeCategory.Primitive);  
            // TODO ByteSegment + ArraySegment<byte>
            // TODO IntPtr + UIntPtr
            else if (itemType.IsEnum || (Nullable.GetUnderlyingType(itemType)?.IsEnum ?? false))
            {
                if (!itemType.IsNullable()) this.InvokeGenericMethod(nameof(CreateEnumReader), new Type[] { itemType }, cachedTypeReader);
                else this.InvokeGenericMethod(nameof(CreateNullableEnumReader), new Type[] { Nullable.GetUnderlyingType(itemType) }, cachedTypeReader);
            }
            else if (itemType == typeof(object)) CreateUnknownObjectReader(cachedTypeReader);
            else if (TryCreateDictionaryTypeReader(itemType, cachedTypeReader)) { }
            else if (TryCreateEnumerableTypeReader(itemType, cachedTypeReader)) { }
            else this.InvokeGenericMethod(nameof(CreateComplexTypeReader), new Type[] { itemType }, cachedTypeReader, true);

            return cachedTypeReader;
        }

        private void CreateByteArrayTypeReader(CachedTypeReader cachedTypeReader)
        {
            CachedTypeReader byteArrayReader = new CachedTypeReader(this);
            this.InvokeGenericMethod(nameof(CreateGenericArrayTypeReader), new Type[] { typeof(byte) }, byteArrayReader);

            cachedTypeReader.SetTypeReader<byte[]>(() =>
            {
                SkipWhiteSpaces();
                byte b = buffer.CurrentByte;
                if (b == '"')
                {
                    if (!TryReadStringBytes(out ByteSegment base64Uft8)) throw new Exception("Expected base64 encoded byte array, but failed on reading the string");

#if NETSTANDARD2_0
                    string base64String = Encoding.UTF8.GetString(base64Uft8.AsArraySegment.Array, base64Uft8.AsArraySegment.Offset, base64Uft8.AsArraySegment.Count);
                    return Convert.FromBase64String(base64String);
#else
                    ReadOnlySpan<byte> utf8Base64 = base64Uft8.AsArraySegment.AsSpan();
                    int maxDecodedLength = Base64.GetMaxDecodedFromUtf8Length(utf8Base64.Length);
                    byte[] bytes = new byte[maxDecodedLength];
                    Span<byte> decodedSpan = bytes;
                    OperationStatus status = Base64.DecodeFromUtf8(utf8Base64, decodedSpan, out int bytesConsumed, out int bytesWritten);
                    if (status != OperationStatus.Done) throw new FormatException($"Invalid Base64 sequence (status = {status}).");
                    if (bytesWritten != bytes.Length) Array.Resize(ref bytes, bytesWritten);
                    return bytes;
#endif
                }
                else if (b == '[')
                {
                    return byteArrayReader.ReadItem<byte[]>();
                }

                throw new Exception("Expected byte array, but didn't got an array nor an Base64 string");
            }, JsonDataTypeCategory.Array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetItemRefInCurrentItemInfo(object item)
        {
            var itemInfo = itemInfos[currentItemInfoIndex];
            itemInfo.itemRef = item;
            itemInfos[currentItemInfoIndex] = itemInfo;
        }

        private Func<T> GetConstructor<T>(Type derivedType = null)
        {
            Func<T> constructor = null;
            Type type = derivedType ?? typeof(T);
            if (settings.constructors.TryGetValue(type, out object c) && c is Func<T> typedConstructor) constructor = () => typedConstructor();
            else if (type.IsValueType) return () => default;
            else if (!TryCompileConstructor<T>(out constructor, derivedType))
            {
                throw new Exception($"No default constructor for type {TypeNameHelper.GetSimplifiedTypeName(type)}.");
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
                throw new Exception($"No constructor for type {TypeNameHelper.GetSimplifiedTypeName(type)} with parameter {TypeNameHelper.GetSimplifiedTypeName(typeof(P))}. Use AddConstructorWithParameter in Settings.");
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

        private void CreateEnumReader<T>(CachedTypeReader cachedTypeReader) where T : struct, Enum
        {
            cachedTypeReader.SetTypeReader(() =>
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
            }, JsonDataTypeCategory.Primitive);
        }

        private void CreateNullableEnumReader<T>(CachedTypeReader cachedTypeReader) where T : struct, Enum
        {
            cachedTypeReader.SetTypeReader<T?>(() =>
            {
                if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;

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
            }, JsonDataTypeCategory.Primitive);
        }

        private object ReadUnknownValue()
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
                case TypeResult.Object: return ReadObjectValueAsDictionary();
                case TypeResult.Bool: return ReadBoolValue();
                case TypeResult.Null: return ReadNullValue();
                case TypeResult.Array: return ReadArrayValueAsList();
                case TypeResult.Number: return ReadNumberValueAsObject();
                default: throw new Exception("Invalid character for determining value");
            }
        }

        CachedTypeReader cachedStringObjectDictionaryReader = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Dictionary<string, object> ReadObjectValueAsDictionary()
        {
            if (cachedStringObjectDictionaryReader == null) cachedStringObjectDictionaryReader = CreateCachedTypeReader(typeof(Dictionary<string, object>));
            return cachedStringObjectDictionaryReader.ReadItem<Dictionary<string, object>>();
        }

        private bool TryCreateDictionaryTypeReader(Type itemType, CachedTypeReader cachedTypeReader)
        {
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

            this.InvokeGenericMethod(nameof(CreateDictionaryTypeReader), new Type[] { itemType, keyType, valueType }, cachedTypeReader);

            return true;
        }

        private void CreateDictionaryTypeReader<T, K, V>(CachedTypeReader cachedTypeReader) where T : IDictionary<K, V>, new()
        {
            if (typeof(T).IsAbstract)
            {
                cachedTypeReader.MakeAbstract<T>(JsonDataTypeCategory.Object);
                return;
            }

            var constructor = GetConstructor<T>();
            var elementReader = new ElementReader<KeyValuePair<K, V>>(this);
            var keyReader = GetCachedTypeReader(typeof(K));
            var valueReader = GetCachedTypeReader(typeof(V));
            var keyValuePairReader = GetCachedTypeReader(typeof(KeyValuePair<K, V>));
            bool isValueRefType = typeof(V).IsByRef;
            bool canValueBePopulated = CanTypeBePopulated(typeof(V));
            List<K> keysToKeep = new List<K>();
            ByteSegment keyProperty = "Key".ToByteArray();
            ByteSegment keyField = "key".ToByteArray();
            ByteSegment valueProperty = "Value".ToByteArray();
            ByteSegment valueField = "value".ToByteArray();


            cachedTypeReader.SetTypeReader(() =>
            {
                T dict = constructor();
                byte b = SkipWhiteSpaces();
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
                        V value = valueReader.ReadValue<V>(fieldNameBytes);
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
            }, JsonDataTypeCategory.Object);

            if (canValueBePopulated)
            {
                List<KeyValuePair<K, V>> keyValueList = new();
                cachedTypeReader.SetPopulatingTypeReader<T>((itemToPopulate) =>
                {
                    T dict = itemToPopulate;                    
                    try
                    {
                        byte b = SkipWhiteSpaces();
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
                                if (dict.TryGetValue(fieldName, out V value)) value = valueReader.ReadValue<V>(fieldNameBytes, value);                                
                                else value = valueReader.ReadValue<V>(fieldNameBytes);                                
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
                                if (!TryReadStringBytes(out var keyFieldName)) throw new Exception("Failed reading KeyValuePair for Dictionary");
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
                                if (!TryReadStringBytes(out var valueFieldName)) throw new Exception("Failed reading KeyValuePair for Dictionary");
                                if (valueFieldName != valueField && valueFieldName != valueProperty) throw new Exception("Failed reading KeyValuePair for Dictionary");
                                b = SkipWhiteSpaces();
                                if (b != ':') throw new Exception("Failed reading KeyValuePair for Dictionary");
                                buffer.TryNextByte();
                                // If the field name exists in dictionary, populate its value, otherwise add it as new 
                                if (dict.TryGetValue(fieldName, out V value)) value = valueReader.ReadValue<V>(fieldNameBytes, value);
                                else value = valueReader.ReadValue<V>(fieldNameBytes);
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
                }, JsonDataTypeCategory.Object);
            }
            else
            {
                cachedTypeReader.SetPopulatingTypeReader<T>((itemToPopulate) =>
                {
                    T dict = itemToPopulate;
                    dict.Clear();

                    byte b = SkipWhiteSpaces();
                    if (b == '{')
                    {
                        if (!buffer.TryNextByte()) throw new Exception("Failed reading Object to Dictionary");
                        while (true)
                        {
                            b = SkipWhiteSpaces();
                            if (b == '}') break;

                            K fieldName = keyReader.ReadFieldName<K>(out var fieldNameBytes); // TODO: Check what to do to make integers and other types work as a dictionary key
                            keysToKeep.Add(fieldName);
                            b = SkipWhiteSpaces();
                            if (b != ':') throw new Exception("Failed reading object to Dictionary");
                            buffer.TryNextByte();
                            V value = valueReader.ReadValue<V>(fieldNameBytes);
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
                }, JsonDataTypeCategory.Object);
            }
        
        }

        private void CreateUnknownObjectReader(CachedTypeReader cachedTypeReader)
        {
            if (!settings.multiOptionTypeMapping.TryGetValue(typeof(object), out var typeOptions))
            {
                cachedTypeReader.SetTypeReader(ReadUnknownValue, JsonDataTypeCategory.Object);
                return;
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
                cachedTypeReader.SetTypeReader(ReadUnknownValue, JsonDataTypeCategory.Object);
                return;
            }

            if (arrayTypeOption == null) arrayTypeOption = typeof(List<object>);
            objectTypeOptions.Add(typeof(Dictionary<string, object>));

            var arrayReader = GetCachedTypeReader(arrayTypeOption);
            CachedTypeReader objectReader = new CachedTypeReader(this);
            CreateMultiOptionComplexTypeReader<object>(objectReader, objectTypeOptions.ToArray());

            var typeReader = () =>
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
                    case TypeResult.Object: return objectReader.ReadItem<object>();
                    case TypeResult.Bool: return ReadBoolValue();
                    case TypeResult.Null: return ReadNullValue();
                    case TypeResult.Array: return arrayReader.ReadItem<object>();
                    case TypeResult.Number: return ReadNumberValueAsObject();
                    default: throw new Exception("Invalid character for determining value");
                }
            };

            cachedTypeReader.SetTypeReader(typeReader, JsonDataTypeCategory.Object);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Buffer.UndoReadHandle CreateUndoReadHandle(bool initUndo = true) => new Buffer.UndoReadHandle(buffer, initUndo);

        private void CreateMultiOptionComplexTypeReader<T>(CachedTypeReader cachedTypeReader, Type[] typeOptions)
        {

            Type[] objectTypeOptions = typeOptions
                .Where(t => !t.IsPrimitive && !t.ImplementsGenericInterface(typeof(IDictionary<,>)))
                .ToArray();            

            CachedTypeReader[] objectTypeReaders = objectTypeOptions
                .Select(t =>
                {
                    if (typeof(T) == t)                     
                    {
                        var selftypeReader = new CachedTypeReader(this);
                        CreateComplexTypeReader<T>(selftypeReader, false);
                        return selftypeReader;
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
                    var itemFieldName = new ByteSegment(name.ToByteArray());                    
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

            Func<T> typeReader = () =>
            {
                byte b = SkipWhiteSpaces();
                var ratings = ratingsPool.Take();
                ratings.AddRange(Enumerable.Repeat(0, numOptions));

                if (b != '{') throw new Exception("Failed reading object");
                int selectionIndex = -1;
                int fallbackIndex = -1;
                int selectionRating = 0;

                using (CreateUndoReadHandle())
                {                    
                    buffer.TryNextByte();                    

                    while (true)
                    {
                        b = SkipWhiteSpaces();
                        if (b == '}') break;

                        if (!TryReadStringBytes(out var fieldName)) throw new Exception("Failed reading object");
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
                        return dictTypeReader.ReadItem<T>();
                    }
                    else
                    {
                        SkipObject();
                        return default;
                    }
                }
                return objectTypeReaders[selectionIndex].ReadItem<T>();
            };

            cachedTypeReader.SetTypeReader(typeReader, JsonDataTypeCategory.Object);
        }        

        private void CreateComplexTypeReader<T>(CachedTypeReader cachedTypeReader, bool checkForMultiOptions)
        {
            Type itemType = typeof(T);


            if (checkForMultiOptions && settings.multiOptionTypeMapping.TryGetValue(itemType, out Type[] mappedTypeOptions))
            {
                this.InvokeGenericMethod(nameof(CreateMultiOptionComplexTypeReader), new Type[] { itemType }, cachedTypeReader, mappedTypeOptions);
                return;
            }

            if (itemType.IsAbstract)
            {
                cachedTypeReader.MakeAbstract<T>(JsonDataTypeCategory.Object);
                return;
            }
            List<MemberInfo> memberInfos = CreateMemberInfosList(itemType);

            Dictionary<ByteSegment, int> itemFieldWritersIndexLookup = new();
            List<(ByteSegment name, Func<T, T> itemFieldWriter)> itemFieldWritersList = new();
            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                string name = memberInfo.Name;
                var itemFieldName = new ByteSegment(name.ToByteArray());
                var itemFieldWriter = this.InvokeGenericMethod<Func<T, T>>(nameof(CreateItemFieldWriter), new Type[] { itemType, fieldType, itemType }, memberInfo, itemFieldName);
                itemFieldWritersIndexLookup[itemFieldName] = itemFieldWritersList.Count;
                itemFieldWritersList.Add((itemFieldName, itemFieldWriter));

                if (name.TryExtract("<{name}>k__BackingField", out string backingFieldName))
                {
                    name = backingFieldName;
                    itemFieldName = new ByteSegment(name.ToByteArray());
                    itemFieldWritersIndexLookup[itemFieldName] = itemFieldWritersList.Count;
                    itemFieldWritersList.Add((itemFieldName, itemFieldWriter));
                }
            }
            var constructor = GetConstructor<T>();

            bool TryFindFieldWriter(ByteSegment fieldName, ref int expectedFieldIndex, out Func<T, T> fieldWriter)
            {
                if (itemFieldWritersList.Count == 0)
                {
                    fieldWriter = null;
                    return false;
                }
                (ByteSegment name, fieldWriter) = itemFieldWritersList[expectedFieldIndex];
                if (name == fieldName)
                {
                    expectedFieldIndex = (expectedFieldIndex + 1) % itemFieldWritersList.Count;
                    return true;
                }
                if (itemFieldWritersIndexLookup.TryGetValue(fieldName, out int index))
                {
                    expectedFieldIndex = (index + 1) % itemFieldWritersList.Count;
                    (_, fieldWriter) = itemFieldWritersList[index];
                    return true;
                }
                return false;
            }

            Func<T> typeReader = () =>
            {
                T item = constructor();
                int expectedFieldIndex = 0;

                byte b = SkipWhiteSpaces();
                if (b != '{') throw new Exception("Failed reading object");
                buffer.TryNextByte();

                while (true)
                {
                    b = SkipWhiteSpaces();
                    if (b == '}') break;

                    if (!TryReadStringBytes(out var fieldName)) throw new Exception("Failed reading object");
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

            cachedTypeReader.SetTypeReader(typeReader, JsonDataTypeCategory.Object);

            Func<T, T> populatingTypeReader = (itemToPopulate) =>
            {
                T item = itemToPopulate;
                int expectedFieldIndex = 0;

                byte b = SkipWhiteSpaces();
                if (b != '{') throw new Exception("Failed reading object");
                buffer.TryNextByte();

                while (true)
                {
                    b = SkipWhiteSpaces();
                    if (b == '}') break;

                    if (!TryReadStringBytes(out var fieldName)) throw new Exception("Failed reading object");
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

            cachedTypeReader.SetPopulatingTypeReader(populatingTypeReader, JsonDataTypeCategory.Object);
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
                    // Use expression tree for writable property
                    return CreatePropertyWriterUsingExpression<T, V, C>(propertyInfo, fieldName);
                }
                else if (HasInitAccessor(propertyInfo))
                {
                    // Handle init-only properties
                    return CreateFieldWriterForInitOnlyProperties<T, V, C>(fieldName, itemType, fieldType, propertyInfo);
                }
            }

            throw new InvalidOperationException("MemberInfo must be a writable field, property, or init-only property.");
        }

        private Func<C, C> CreateFieldWriterForInitOnlyProperties<T, V, C>(ByteSegment fieldName, Type itemType, Type fieldType, PropertyInfo propertyInfo) where T : C
        {            
            var fieldTypeReader = GetCachedTypeReader(fieldType);
            if (itemType.IsValueType)
            {
                if (CanTypeBePopulated(itemType) && propertyInfo.CanRead)
                {
                    return parentItem =>
                    {
                        V value = (V)propertyInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                        value = fieldTypeReader.ReadValue<V>(fieldName, value);
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
                        V value = fieldTypeReader.ReadValue<V>(fieldName);
                        var boxedItem = (object)parentItem;
                        propertyInfo.SetValue(boxedItem, value);
                        parentItem = (T)boxedItem;
                        return parentItem;
                    };
                }
            }
            else
            {
                if (CanTypeBePopulated(itemType) && propertyInfo.CanRead)
                {
                    return parentItem =>
                    {
                        if (TryReadNullValue())
                        {
                            propertyInfo.SetValue(parentItem, null);
                        }
                        else
                        {
                            V value = (V)propertyInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                            value = fieldTypeReader.ReadValue<V>(fieldName, value);
                            propertyInfo.SetValue(parentItem, value);
                        }                        
                        return parentItem;
                    };
                }
                else
                {
                    return parentItem =>
                    {
                        V value = fieldTypeReader.ReadValue<V>(fieldName);
                        propertyInfo.SetValue(parentItem, value);
                        return parentItem;
                    };
                }
            }
        }

        private Func<C, C> CreateFieldWriterForInitOnlyFields<T, V, C>(ByteSegment fieldName, Type itemType, Type fieldType, FieldInfo fieldInfo) where T : C
        {            
            var fieldTypeReader = GetCachedTypeReader(fieldType);
            if (itemType.IsValueType)
            {
                if (CanTypeBePopulated(itemType))
                {
                    return parentItem =>
                    {
                        V value = (V)fieldInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                        value = fieldTypeReader.ReadValue<V>(fieldName, value);
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
                        V value = fieldTypeReader.ReadValue<V>(fieldName);
                        var boxedItem = (object)parentItem;
                        fieldInfo.SetValue(boxedItem, value);
                        parentItem = (T)boxedItem;
                        return parentItem;
                    };
                }
            }
            else
            {
                if (CanTypeBePopulated(itemType))
                {
                    return parentItem =>
                    {
                        if (TryReadNullValue())
                        {
                            fieldInfo.SetValue(parentItem, null);
                        }
                        else
                        {
                            V value = (V)fieldInfo.GetValue(parentItem); // TODO: can be optimized via Expression
                            value = fieldTypeReader.ReadValue<V>(fieldName, value);
                            fieldInfo.SetValue(parentItem, value);
                        }
                        return parentItem;
                    };
                }
                else
                {
                    return parentItem =>
                    {
                        V value = fieldTypeReader.ReadValue<V>(fieldName);
                        fieldInfo.SetValue(parentItem, value);
                        return parentItem;
                    };
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

                if (CanTypeBePopulated(fieldType))
                {
                    return parentItem =>
                    {
                        V fieldValue = getValue((T)parentItem);
                        fieldValue = fieldTypeReader.ReadValue<V>(fieldName, fieldValue);
                        parentItem = setValueAndReturn((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadValue<V>(fieldName);
                        parentItem = setValueAndReturn((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
            }
            else
            {
                BinaryExpression assignExpression = Expression.Assign(memberAccess, value);
                var lambdaSetter = Expression.Lambda<Action<T, V>>(assignExpression, target, value);
                Action<T, V> setValue = lambdaSetter.Compile();

                var lambdaGetter = Expression.Lambda<Func<T, V>>(memberAccess, target);
                Func<T, V> getValue = lambdaGetter.Compile();
                if (CanTypeBePopulated(fieldType))
                {
                    return parentItem =>
                    {
                        if (TryReadNullValue())
                        {
                            setValue((T)parentItem, default);
                        }
                        else
                        {
                            V fieldValue = getValue((T)parentItem);
                            fieldValue = fieldTypeReader.ReadValue<V>(fieldName, fieldValue);
                            setValue((T)parentItem, fieldValue);
                        }
                        return parentItem;
                    };
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadValue<V>(fieldName);
                        setValue((T)parentItem, fieldValue);
                        return parentItem;
                    };
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

                if (CanTypeBePopulated(fieldType) && propertyInfo.CanRead)
                {
                    return parentItem =>
                    {
                        V fieldValue = getValue((T)parentItem);
                        fieldValue = fieldTypeReader.ReadValue<V>(fieldName, fieldValue);
                        parentItem = setValueAndReturn((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadValue<V>(fieldName);
                        parentItem = setValueAndReturn((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
            }
            else
            {
                BinaryExpression assignExpression = Expression.Assign(memberAccess, value);
                var lambdaSetter = Expression.Lambda<Action<T, V>>(assignExpression, target, value);
                Action<T, V> setValue = lambdaSetter.Compile();

                var lambdaGetter = Expression.Lambda<Func<T, V>>(memberAccess, target);
                Func<T, V> getValue = lambdaGetter.Compile();

                if (CanTypeBePopulated(fieldType) && propertyInfo.CanRead)
                {
                    return parentItem =>
                    {
                        if (TryReadNullValue())
                        {
                            setValue((T)parentItem, default);
                        }
                        else
                        {
                            V fieldValue = getValue((T)parentItem);
                            fieldValue = fieldTypeReader.ReadValue<V>(fieldName, fieldValue);
                            setValue((T)parentItem, fieldValue);
                        }
                        return parentItem;
                    };
                }
                else
                {
                    return parentItem =>
                    {
                        V fieldValue = fieldTypeReader.ReadValue<V>(fieldName);
                        setValue((T)parentItem, fieldValue);
                        return parentItem;
                    };
                }
            }
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

        private void CreateArrayTypeReader(Type arrayType, CachedTypeReader cachedTypeReader)
        {
            if (arrayType == typeof(byte[]))
            {
                CreateByteArrayTypeReader(cachedTypeReader);
            }
            else 
            {
                this.InvokeGenericMethod(nameof(CreateGenericArrayTypeReader), new Type[] { arrayType.GetElementType() }, cachedTypeReader);
            }
        }

        private void CreateGenericArrayTypeReader<E>(CachedTypeReader cachedTypeReader)
        {
            var elementReader = new ElementReader<E>(this);
            Pool<List<E>> pool = new Pool<List<E>>(() => new List<E>(), l => l.Clear(), 1000, false);
            cachedTypeReader.SetTypeReader(() =>
            {
                byte b = SkipWhiteSpaces();
                if (b != '[') throw new Exception("Failed reading Array");
                if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
                List<E> elementBuffer = pool.Take();
                elementBuffer.AddRange(elementReader);
                E[] item = elementBuffer.ToArray();
                if (settings.enableReferenceResolution) SetItemRefInCurrentItemInfo(item);
                pool.Return(elementBuffer);
                if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
                buffer.TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);

        }

        private bool TryCreateEnumerableTypeReader(Type itemType, CachedTypeReader cachedTypeReader)
        {
            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IList<>), out Type elementType))
            {
                var enumerableType = typeof(IList<>).MakeGenericType(elementType);                
                this.InvokeGenericMethod(nameof(CreateGenericListTypeReader), new Type[] { itemType, elementType }, cachedTypeReader);
            }
            else if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out elementType))
            {
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
                this.InvokeGenericMethod(nameof(CreateGenericEnumerableTypeReader), new Type[] { itemType, elementType }, cachedTypeReader);
            }
            else if (itemType.ImplementsInterface(typeof(IEnumerable)))
            {
                this.InvokeGenericMethod(nameof(CreateEnumerableTypeReader), new Type[] { itemType }, cachedTypeReader);
            }
            else
            {
                return false;
            }

            return true;
        }

        private void CreateGenericListTypeReader<T, E>(CachedTypeReader cachedTypeReader) where T : IList<E>
        {
            if (typeof(T).IsAbstract)
            {
                cachedTypeReader.MakeAbstract<T>(JsonDataTypeCategory.Object);
                return;
            }

            var elementReader = new ElementReader<E>(this);

            var constructor = GetConstructor<T>();
            
            cachedTypeReader.SetTypeReader<T>(() =>
            {
                byte b = SkipWhiteSpaces();
                if (b != '[') throw new Exception("Failed reading Array");
                if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
                
                T item = constructor();
                while (elementReader.MoveNext())
                {
                    item.Add(elementReader.Current);
                }

                if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
                buffer.TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);
        }

        private void CreateGenericEnumerableTypeReader<T, E>(CachedTypeReader cachedTypeReader)
        {
            if (typeof(T).IsAbstract)
            {
                cachedTypeReader.MakeAbstract<T>(JsonDataTypeCategory.Object);
                return;
            }

            var constructor = GetConstructor<T, IEnumerable<E>>();

            Pool<ElementReader<E>> elementReaderPool = new Pool<ElementReader<E>>(() => new ElementReader<E>(this), l => l.Reset(), 1000, false);
            Pool<List<E>> bufferPool = new Pool<List<E>>(() => new List<E>(), l => l.Clear(), 1000, false);
            cachedTypeReader.SetTypeReader(() =>
            {
                byte b = SkipWhiteSpaces();
                if (b != '[') throw new Exception("Failed reading Array");
                if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
                ElementReader<E> elementReader = elementReaderPool.Take();
                List<E> elementBuffer = bufferPool.Take();
                elementBuffer.AddRange(elementReader);
                T item = constructor(elementBuffer);
                bufferPool.Return(elementBuffer);
                if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
                buffer.TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);
        }

        private void CreateEnumerableTypeReader<T>(CachedTypeReader cachedTypeReader)
        {
            if (typeof(T).IsAbstract)
            {
                cachedTypeReader.MakeAbstract<T>(JsonDataTypeCategory.Object);
                return;
            }

            Func<IEnumerable, T> constructor = GetConstructor<T, IEnumerable>();
            Pool<ElementReader<object>> elementReaderPool = new Pool<ElementReader<object>>(() => new ElementReader<object>(this), l => l.Reset(), 1000, false);
            Pool<List<object>> pool = new Pool<List<object>>(() => new List<object>(), l => l.Clear(), 1000, false);
            cachedTypeReader.SetTypeReader<T>(() =>
            {
                byte b = SkipWhiteSpaces();
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
            }, JsonDataTypeCategory.Array);
        }

        List<ByteSegment> arrayElementNameCache = new List<ByteSegment>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ByteSegment GetArrayElementName(int index)
        {
            while (arrayElementNameCache.Count <= index)
            {
                arrayElementNameCache.Add(new ByteSegment($"[{index}]".ToByteArray()));
            }
            return arrayElementNameCache[index];
        }

        private class ElementReader<T> : IEnumerable<T>, IEnumerator<T>, IEnumerable, IEnumerator
        {
            FeatureJsonDeserializer deserializer;
            CachedTypeReader reader;
            T current = default;
            int index = -1;
            readonly bool enableReferenceResolution;

            public ElementReader(FeatureJsonDeserializer deserializer)
            {
                this.deserializer = deserializer;
                enableReferenceResolution = deserializer.settings.enableReferenceResolution;
                reader = deserializer.GetCachedTypeReader(typeof(T));
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
                if (enableReferenceResolution) current = reader.ReadValue<T>(deserializer.GetArrayElementName(++index));
                else current = reader.ReadItem<T>();
                b = deserializer.SkipWhiteSpaces();
                if (b == ',') deserializer.buffer.TryNextByte();
                else if (deserializer.buffer.CurrentByte != ']')
                {
                    throw new Exception("Failed reading Array");
                }
                return true;
            }

            // [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        CollectionCaster listCaster = new CollectionCaster();

        CachedTypeReader cachedObjectListReader = null;
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ReadArrayValueAsList()
        {
            if (cachedObjectListReader == null) cachedObjectListReader = CreateCachedTypeReader(typeof(List<object>));
            var objectsList = cachedObjectListReader.ReadItem<List<object>>();
            if (!settings.tryCastArraysOfUnknownValues || objectsList.Count == 0) return objectsList;
            
            var castedList = listCaster.CastToCommonTypeList(objectsList, out _);
            return castedList;
        }

        private bool TryReadObjectValue<T>(out T value, ByteSegment itemName)
        {
            value = default;
            try
            {
                var typeReader = GetCachedTypeReader(typeof(T));                
                if (typeReader.JsonTypeCategory != JsonDataTypeCategory.Object) return false;
                if (itemName.IsEmptyOrInvalid) value = typeReader.ReadItem<T>();
                else value = typeReader.ReadValue<T>(itemName);
            }
            catch
            {
                return false;
            }
            return true;            
        }

        private bool TryReadArrayValue<T>(out T value, ByteSegment itemName) where T : IEnumerable
        {
            value = default;
            try
            {
                var typeReader = GetCachedTypeReader(typeof(T));
                if (typeReader.JsonTypeCategory != JsonDataTypeCategory.Array) return false;
                if (itemName.IsEmptyOrInvalid) value = typeReader.ReadItem<T>();
                else value = typeReader.ReadValue<T>(itemName);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private string ReadStringValue()
        {
            if (!TryReadStringBytes(out var stringBytes)) throw new Exception("Failed reading string");
            string result = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);
            stringBuilder.Clear();
            return result;
        }

        private bool TryReadStringValue(out string value)
        {
            value = null;
            if (!TryReadStringBytes(out var stringBytes)) return false;
            value = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);
            stringBuilder.Clear();
            return true;
        }

        private char ReadCharValue()
        {
            if (!TryReadStringBytes(out var stringBytes)) throw new Exception("Failed reading string");
            Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
            if (stringBuilder.Length == 0) throw new Exception("string for reading char is empty");
            char c = stringBuilder[0];
            stringBuilder.Clear();
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char? ReadNullableCharValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadCharValue();
        }

        private DateTime ReadDateTimeValue()
        {            
            if (!TryReadStringBytes(out var stringBytes))
            {                
                if (!settings.strict && TryReadNullValue())
                {                    
                    return default;
                }
                else throw new Exception("Failed reading DateTime (not a string)");
            }

            if (stringBytes.Count == 0 && !settings.strict)
            {
                return default;
            }            

            DateTime result;
#if NET5_0_OR_GREATER
            Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
            ReadOnlySpan<char> span = new ReadOnlySpan<char>();                        
            foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
            {
                if (span.IsEmpty) span = chunk.Span; // First chunk, that is good
                else
                {
                    // second chunk is bad and we need to reset to fall back to copying (This is very unlikely for a DateTime string)
                    span = new ReadOnlySpan<char>();
                    break;
                }
            }
            if (span.IsEmpty)
            {
                var chars = charSlicedBuffer.GetSlice(stringBuilder.Length);
                stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
                span = chars;
                charSlicedBuffer.Reset(true); // We reset early, though the slice/span was not used yet. That works because the underlying array is not erased.
            }
            result = DateTime.Parse(span);            
#elif NETSTANDARD2_1_OR_GREATER
            ReadOnlySpan<char> span = Utf8Converter.DecodeUtf8ToSpanOfChars(stringBytes, stringBuilder, charSlicedBuffer);            
            result = DateTime.Parse(span);            
            charSlicedBuffer.Reset(true);
#else
            string str = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);            
            result = DateTime.Parse(str);                        
#endif
            stringBuilder.Clear();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DateTime? ReadNullableDateTimeValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadDateTimeValue();
        }

        private TimeSpan ReadTimeSpanValue()
        {
            if (!TryReadStringBytes(out var stringBytes))
            {
                if (!settings.strict && TryReadNullValue())
                {
                    return default;
                }
                else throw new Exception("Failed reading TimeSpan (not a string)");
            }

            if (stringBytes.Count == 0 && !settings.strict)
            {
                return default;
            }

            TimeSpan result;
#if NET5_0_OR_GREATER
            Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
            ReadOnlySpan<char> span = new ReadOnlySpan<char>();                        
            foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
            {
                if (span.IsEmpty) span = chunk.Span; // First chunk, that is good
                else
                {
                    // second chunk is bad and we need to reset to fall back to copying (This is very unlikely for a TimeSpan string)
                    span = new ReadOnlySpan<char>();
                    break;
                }
            }
            if (span.IsEmpty)
            {
                var chars = charSlicedBuffer.GetSlice(stringBuilder.Length);
                stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
                span = chars;
                charSlicedBuffer.Reset(true); // We reset early, though the slice/span was not used yet. That works because the underlying array is not erased.
            }
            result = TimeSpan.Parse(span);            
#elif NETSTANDARD2_1_OR_GREATER
            ReadOnlySpan<char> span = Utf8Converter.DecodeUtf8ToSpanOfChars(stringBytes, stringBuilder, charSlicedBuffer);            
            result = TimeSpan.Parse(span);            
            charSlicedBuffer.Reset(true);
#else
            string str = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);
            result = TimeSpan.Parse(str);
#endif
            stringBuilder.Clear();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TimeSpan? ReadNullableTimeSpanValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadTimeSpanValue();
        }

        private Guid ReadGuidValue()
        {
            if (!TryReadStringBytes(out var stringBytes)) throw new Exception("Failed reading DatTime");
            Guid result;
#if NET5_0_OR_GREATER
            Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
            ReadOnlySpan<char> span = new ReadOnlySpan<char>();
            foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
            {
                if (span.IsEmpty) span = chunk.Span; // First chunk, that is good
                else
                {
                    // second chunk is bad and we need to reset to fall back to copying (This is very unlikely for a Guid string)
                    span = new ReadOnlySpan<char>();
                    break;
                }
            }
            if (span.IsEmpty)
            {
                var chars = charSlicedBuffer.GetSlice(stringBuilder.Length);
                stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
                span = chars;
                charSlicedBuffer.Reset(true); // We reset early, though the slice/span was not used yet. That works because the underlying array is not erased.
            }
            result = Guid.Parse(span);
#elif NETSTANDARD2_1_OR_GREATER
            ReadOnlySpan<char> span = Utf8Converter.DecodeUtf8ToSpanOfChars(stringBytes, stringBuilder, charSlicedBuffer);            
            result = Guid.Parse(span);            
            charSlicedBuffer.Reset(true);
#else
            string str = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);            
            result = Guid.Parse(str);                        
#endif
            stringBuilder.Clear();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Guid? ReadNullableGuidValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadGuidValue();
        }

        StringBuilder stringBuilder = new StringBuilder(1024 * 8);
        SlicedBuffer<char> charSlicedBuffer = new SlicedBuffer<char>(1024 * 4, 1024 * 16, 2, true, false);

        private object ReadNullValue()
        {
            byte b = SkipWhiteSpaces();
            if (b != 'n' && b != 'N') throw new Exception("Failed reading null");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading null");
            b = buffer.CurrentByte;
            if (b != 'u' && b != 'U') throw new Exception("Failed reading null");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading null");
            b = buffer.CurrentByte;
            if (b != 'l' && b != 'L') throw new Exception("Failed reading null");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading null");
            b = buffer.CurrentByte;
            if (b != 'l' && b != 'L') throw new Exception("Failed reading null");

            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading null");
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReadBoolValue()
        {
            if (!TryReadBoolValue(out bool value)) throw new Exception("Failed reading boolean");
            return value;
        }

        private bool TryReadBoolValue(out bool value)
        {
            value = default;            
            using (var undoHandle = CreateUndoReadHandle())
            {
                value = default;

                byte b = SkipWhiteSpaces();
                b = buffer.CurrentByte;
                if (b == 't' || b == 'T')
                {
                    if (!buffer.TryNextByte()) return false;
                    b = buffer.CurrentByte;
                    if (b != 'r' && b != 'R') return false;

                    if (!buffer.TryNextByte()) return false;
                    b = buffer.CurrentByte;
                    if (b != 'u' && b != 'U') return false;

                    if (!buffer.TryNextByte()) return false;
                    b = buffer.CurrentByte;
                    if (b != 'e' && b != 'E') return false;

                    if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;
                    value = true;
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                else if (b == 'f' || b == 'F')
                {
                    if (!buffer.TryNextByte()) return false;
                    b = buffer.CurrentByte;
                    if (b != 'a' && b != 'A') return false;

                    if (!buffer.TryNextByte()) return false;
                    b = buffer.CurrentByte;
                    if (b != 'l' && b != 'L') return false;

                    if (!buffer.TryNextByte()) return false;
                    b = buffer.CurrentByte;
                    if (b != 's' && b != 'S') return false;

                    if (!buffer.TryNextByte()) return false;
                    b = buffer.CurrentByte;
                    if (b != 'e' && b != 'E') return false;

                    if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;
                    value = false;
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                else return false;
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool? ReadNullableBoolValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadBoolValue();
        }

        private object ReadNumberValueAsObject()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative, ValidNumberComponents.all)) throw new Exception("Failed reading number");
            if (decimalBytes.IsValid || isExponentNegative)
            {
                ulong integerPart = integerBytes.AsArraySegment.EmptyOrNull() ? 0 : BytesToInteger(integerBytes);
                double decimalPart = decimalBytes.AsArraySegment.EmptyOrNull() ? 0 : BytesToInteger(decimalBytes);
                double value = ApplyExponent(decimalPart, -decimalBytes.Count);
                value += integerPart;
                if (isNegative) value *= -1;

                if (exponentBytes.IsValid)
                {
                    int exp = (int)BytesToInteger(exponentBytes);
                    if (isExponentNegative) exp = -exp;
                    value = ApplyExponent(value, exp);
                }

                return value;
            }
            else
            {
                ulong integerPart = integerBytes.AsArraySegment.EmptyOrNull() ? 0 : BytesToInteger(integerBytes);
                if (exponentBytes.IsValid)
                {
                    int exp = (int)BytesToInteger(exponentBytes);
                    integerPart = ApplyExponent(integerPart, exp);                    
                }

                if (isNegative)
                {
                    long value = -(long)integerPart;
                    if (value < int.MinValue) return value;
                    return (int)value;
                }
                else
                {
                    if (integerPart > long.MaxValue) return integerPart;
                    long value = (long)integerPart;
                    if (value > int.MaxValue) return value;
                    return (int)value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadLongValue()
        {
            if (!TryReadSignedIntegerValue(out long value)) throw new Exception("Failed reading integer number");
            return value;
        }

        public bool TryReadSignedIntegerValue(out long value)
        {
            value = default;
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative, ValidNumberComponents.signedInteger)) return false;

            ulong integerPart = BytesToInteger(integerBytes);

            if (exponentBytes.IsValid)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                if (isExponentNegative) exp = -exp;
                integerPart = ApplyExponent(integerPart, exp);
            }

            value = (long)integerPart;
            if (isNegative) value *= -1;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long? ReadNullableLongValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadLongValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadIntValue()
        {
            long longValue = ReadLongValue();
            if (longValue > int.MaxValue || longValue < int.MinValue) throw new Exception("Value is out of bounds.");
            return (int)longValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int? ReadNullableIntValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadIntValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadShortValue()
        {
            long longValue = ReadLongValue();
            if (longValue > short.MaxValue || longValue < short.MinValue) throw new Exception("Value is out of bounds.");
            return (short)longValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short? ReadNullableShortValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadShortValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSbyteValue()
        {
            long longValue = ReadLongValue();
            if (longValue > sbyte.MaxValue || longValue < sbyte.MinValue) throw new Exception("Value is out of bounds.");
            return (sbyte)longValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte? ReadNullableSbyteValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadSbyteValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUlongValue()
        {
            if (!TryReadUnsignedIntegerValue(out ulong value)) throw new Exception("Failed reading unsigned integer number");
            return value;
        }

        public bool TryReadUnsignedIntegerValue(out ulong value)
        {
            value = default;
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative, ValidNumberComponents.unsignedInteger)) return false;

            value = BytesToInteger(integerBytes);

            if (exponentBytes.IsValid)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                if (isExponentNegative) exp = -exp;
                value = ApplyExponent(value, exp);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong? ReadNullableUlongValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadUlongValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUintValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > uint.MaxValue) throw new Exception("Value is out of bounds.");
            return (uint)longValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint? ReadNullableUintValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadUintValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUshortValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > ushort.MaxValue) throw new Exception("Value is out of bounds.");
            return (ushort)longValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort? ReadNullableUshortValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadUshortValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByteValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > byte.MaxValue) throw new Exception("Value is out of bounds.");
            return (byte)longValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte? ReadNullableByteValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadByteValue();
        }

        ByteSegment SPECIAL_NUMBER_NAN = "NaN".ToByteArray();
        ByteSegment SPECIAL_NUMBER_POS_INFINITY = "Infinity".ToByteArray();
        ByteSegment SPECIAL_NUMBER_NEG_INFINITY = "-Infinity".ToByteArray();

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDoubleValue()
        {
            byte b = SkipWhiteSpaces();
            if (b == (byte)'"')
            {
                using (var undoHandle = CreateUndoReadHandle(false))
                {
                    if (!TryReadStringBytes(out var str)) throw new Exception("Invalid string found for a number: could not read it");
                    if (SPECIAL_NUMBER_NAN.Equals(str)) return double.NaN;
                    if (SPECIAL_NUMBER_POS_INFINITY.Equals(str)) return double.PositiveInfinity;
                    if (SPECIAL_NUMBER_NEG_INFINITY.Equals(str)) return double.NegativeInfinity;

                    if (!settings.strict) undoHandle.SetUndoReading(true);                    
                    else throw new Exception($"Invalid string found for a number: only allowed is NaN, Infinity, -Infinity");
                }
            }
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative, ValidNumberComponents.floatingPointNumber)) throw new Exception("Failed reading number");

            ulong integerPart = integerBytes.AsArraySegment.EmptyOrNull() ? 0 : BytesToInteger(integerBytes);
            double decimalPart = decimalBytes.AsArraySegment.EmptyOrNull() ? 0 : BytesToInteger(decimalBytes);
            double value = ApplyExponent(decimalPart, -decimalBytes.Count);        
            value += integerPart;
            if (isNegative) value *= -1;

            if (exponentBytes.IsValid)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                if (isExponentNegative) exp = -exp;
                value = ApplyExponent(value, exp);
            }

            return value;
        }

        public bool TryReadFloatingPointValue(out double value)
        {
            value = default;
            byte b = SkipWhiteSpaces();
            if (b == (byte)'"')
            {
                using (var undoHandle = CreateUndoReadHandle())
                {                    
                    bool isValidString = TryReadStringBytes(out var str);
                    if (isValidString)
                    {
                        if (SPECIAL_NUMBER_NAN.Equals(str)) value = double.NaN;
                        else if (SPECIAL_NUMBER_POS_INFINITY.Equals(str)) value = double.PositiveInfinity;
                        else if (SPECIAL_NUMBER_NEG_INFINITY.Equals(str)) value = double.NegativeInfinity;
                        else isValidString = false;
                    }
                    undoHandle.SetUndoReading(!isValidString);
                    return isValidString;
                }
            }

            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative, ValidNumberComponents.floatingPointNumber)) return false;

            ulong integerPart = integerBytes.AsArraySegment.EmptyOrNull() ? 0 : BytesToInteger(integerBytes);
            double decimalPart = decimalBytes.AsArraySegment.EmptyOrNull() ? 0 : BytesToInteger(decimalBytes);
            value = ApplyExponent(decimalPart, -decimalBytes.Count);
            value += integerPart;
            if (isNegative) value *= -1;

            if (exponentBytes.IsValid)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                if (isExponentNegative) exp = -exp;
                value = ApplyExponent(value, exp);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double? ReadNullableDoubleValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadDoubleValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimalValue()
        {
            double dbl = ReadDoubleValue();
            if (double.IsNaN(dbl) || double.IsInfinity(dbl)) throw new Exception("Decimals cannot be NaN or Infinity");
            return (decimal)dbl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal? ReadNullableDecimalValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadDecimalValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloatValue() => (float)ReadDoubleValue();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? ReadNullableFloatValue()
        {
            if (TryReadNullValue() || (!settings.strict && TryReadEmptyStringValue())) return null;
            return ReadFloatValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonFragment ReadJsonFragmentValue()
        {
            SkipWhiteSpaces();
            var rec = buffer.StartRecording();
            SkipValue();
            var utf8Bytes = rec.GetRecordedBytes(buffer.IsBufferReadToEnd);
            string json = DecodeUtf8Bytes(utf8Bytes);
            JsonFragment fragment = new JsonFragment(json);
            return fragment;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsonFragment? ReadNullableJsonFragmentValue()
        {
            var fragment = ReadJsonFragmentValue();
            return fragment.IsValid ? fragment : null;
        }

        private string DecodeUtf8Bytes(ArraySegment<byte> bytes)
        {
            string str = Utf8Converter.DecodeUtf8ToString(bytes, stringBuilder);
            stringBuilder.Clear();
            return str;
        }


        CachedTypeReader lastTypeReader = null;
        Type lastTypeReaderType = null;

        public void SetDataSource(Stream stream)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(stream);
            }
            finally 
            { 
                serializerLock.Exit(); 
            }
        }

        public void SetDataSource(string json)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(json);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public void SetDataSource(ByteSegment uft8Bytes)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(uft8Bytes);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        private void SetDataSourceUnlocked(Stream stream) => buffer.SetSource(stream);
        private void SetDataSourceUnlocked(string json) => buffer.SetSource(json);
        private void SetDataSourceUnlocked(ByteSegment jsonBytes) => buffer.SetSource(jsonBytes);

        public bool IsAnyDataLeft()
        {
            serializerLock.Enter();
            try
            {
                return IsAnyDataLeftUnlocked();
            }
            finally 
            { 
                serializerLock.Exit(); 
            }
        }

        private bool IsAnyDataLeftUnlocked()
        {
            ref var map_SkipWhitespaces_ref = ref map_SkipWhitespaces[0];
            byte b;
            if (!buffer.IsBufferReadToEnd)
            {
                // Ignore whitespaces and check if any other character is found
                b = SkipWhiteSpaces();
                if (LookupCheck(ref map_SkipWhitespaces_ref, b, FilterResult.Found)) return true;
            }

            if (buffer.IsBufferCompletelyFilled) buffer.ResetBuffer(true, false);
            if (!buffer.TryReadFromStream()) return false;

            // Ignore whitespaces and check if any other character is found
            b = SkipWhiteSpaces();
            return LookupCheck(ref map_SkipWhitespaces_ref, b, FilterResult.Found);
        }

        private bool TryDeserializeLocked<T>(out T item)
        {
            item = default;
            bool retry = false;
            do
            {
                retry = false;
                try
                {
                    if (!buffer.TryPrepareDeserialization())
                    {
                        item = default;
                        return false;
                    }

                    // Return false if only whitespaces are left (otherwise we would throw an exception)
                    byte b = SkipWhiteSpaces();  
                    if (LookupCheck(map_SkipWhitespaces, b, FilterResult.Skip)) return false;

                    var itemType = typeof(T);
                    if (lastTypeReaderType == itemType)
                    {
                        item = lastTypeReader.ReadValue<T>(rootName);
                    }
                    else
                    {
                        var reader = GetCachedTypeReader(itemType);
                        lastTypeReader = reader;
                        lastTypeReaderType = itemType;
                        item = reader.ReadValue<T>(rootName);
                    }
                    return true;
                }
                catch (BufferExceededException)
                {

                    buffer.ResetAfterBufferExceededException();

                    currentItemName = rootName;                    
                    itemInfos.Clear();
                    currentItemInfoIndex = -1;

                    if (!buffer.TryReadFromStream() && !IsAnyDataLeftUnlocked())
                    {
                        item = default;
                        return false;
                    }

                    retry = true;
                }
                catch (Exception e)
                {
                    if (settings.logCatchedExceptions) OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
                    if (settings.rethrowExceptions) throw;
                }
                finally
                {
                    if (!retry)
                    {
                        Reset();
                    }
                }
            } while (retry);

            return false;
        }

        private bool TryDeserializeLocked(Type itemType, out object item)
        {
            item = default;
            bool retry = false;
            do
            {
                retry = false;
                try
                {
                    if (!buffer.TryPrepareDeserialization())
                    {
                        item = default;
                        return false;
                    }

                    // Return false if only whitespaces are left (otherwise we would throw an exception)
                    byte b = SkipWhiteSpaces();
                    if (LookupCheck(map_SkipWhitespaces, b, FilterResult.Skip)) return false;

                    if (lastTypeReaderType == itemType)
                    {
                        item = lastTypeReader.ReadValue<object>(rootName);
                    }
                    else
                    {
                        var reader = GetCachedTypeReader(itemType);
                        lastTypeReader = reader;
                        lastTypeReaderType = itemType;
                        item = reader.ReadValue<object>(rootName);
                    }
                    return true;
                }
                catch (BufferExceededException)
                {

                    buffer.ResetAfterBufferExceededException();

                    currentItemName = rootName;
                    itemInfos.Clear();
                    currentItemInfoIndex = -1;

                    if (!buffer.TryReadFromStream() && !IsAnyDataLeftUnlocked())
                    {
                        item = default;
                        return false;
                    }

                    retry = true;
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
                    if (settings.rethrowExceptions) throw;
                }
                finally
                {
                    if (!retry)
                    {
                        Reset();
                    }
                }
            } while (retry);

            return false;
        }

        private bool TryPopulateLocked<T>(ref T item)
        {
            bool retry = false;
            do
            {
                isPopulating = true;
                retry = false;
                try
                {
                    if (!buffer.TryPrepareDeserialization())
                    {                        
                        return false;
                    }

                    // Return false if only whitespaces are left (otherwise we would throw an exception)
                    byte b = SkipWhiteSpaces();
                    if (LookupCheck(map_SkipWhitespaces, b, FilterResult.Skip)) return false;
                    
                    var itemType = item != null ? item.GetType() : typeof(T);
                    if (lastTypeReaderType == itemType)
                    {
                        item = lastTypeReader.ReadValue(rootName, item);
                    }
                    else
                    {
                        var reader = GetCachedTypeReader(itemType);
                        lastTypeReader = reader;
                        lastTypeReaderType = itemType;
                        item = reader.ReadValue(rootName, item);
                    }
                    return true;
                }
                catch (BufferExceededException)
                {

                    buffer.ResetAfterBufferExceededException();

                    currentItemName = rootName;
                    itemInfos.Clear();
                    currentItemInfoIndex = -1;

                    if (!buffer.TryReadFromStream() && !IsAnyDataLeftUnlocked())
                    {
                        // At this point the item is probably partially populated
                        return false;
                    }

                    retry = true;
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
                    if (settings.rethrowExceptions) throw;
                }
                finally
                {
                    if (!retry)
                    {
                        Reset();
                    }
                }
            } while (retry);

            return false;
        }
        public bool TryDeserialize<T>(out T item)
        {
            serializerLock.Enter();
            try
            {
                return TryDeserializeLocked(out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryDeserialize(Type type, out object item)
        {
            serializerLock.Enter();
            try
            {
                return TryDeserializeLocked(type, out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryDeserialize<T>(Stream stream, out T item)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(stream);
                return TryDeserializeLocked(out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryDeserialize(Stream stream, Type type, out object item)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(stream);
                return TryDeserializeLocked(type, out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryDeserialize<T>(string json , out T item)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(json);
                return TryDeserializeLocked(out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryDeserialize(string json, Type type, out object item)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(json);
                return TryDeserializeLocked(type, out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryDeserialize<T>(ByteSegment utf8Bytes, out T item)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(utf8Bytes);
                return TryDeserializeLocked(out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryDeserialize(ByteSegment utf8Bytes, Type type, out object item)
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(utf8Bytes);
                return TryDeserializeLocked(type, out item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(ref T item) where T : struct
        {
            serializerLock.Enter();
            try
            {
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(Stream stream, ref T item) where T : struct
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(stream);
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(string json, ref T item) where T : struct
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(json);
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(ByteSegment utf8Bytes, ref T item) where T : struct
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(utf8Bytes);
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(T item) where T : class
        {
            serializerLock.Enter();
            try
            {
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(Stream stream, T item) where T : class
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(stream);
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(string json, T item) where T : class
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(json);
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        public bool TryPopulate<T>(ByteSegment utf8Bytes, T item) where T : class
        {
            serializerLock.Enter();
            try
            {
                SetDataSourceUnlocked(utf8Bytes);
                return TryPopulateLocked(ref item);
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        void SkipValue()
        {
            byte b = SkipWhiteSpaces();

            var valueType = Lookup(map_TypeStart, b);
            switch (valueType)
            {
                case TypeResult.String: SkipString(); break;
                case TypeResult.Object: SkipObject(); break;
                case TypeResult.Bool: SkipBool(); break;
                case TypeResult.Null: SkipNull(); break;
                case TypeResult.Array: SkipArray(); break;
                case TypeResult.Number: SkipNumber(); break;
                default: throw new Exception("Invalid character for value");
            }
        }        
        
        private void SkipNumber()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative, ValidNumberComponents.all)) throw new Exception("Failed reading number");
        }

        private void SkipArray()
        {
            byte b = SkipWhiteSpaces();
            if (b != '[') throw new Exception("Failed reading array");
            if (!buffer.TryNextByte()) throw new Exception("Failed reading array");
            b = SkipWhiteSpaces();
            while (b != ']')
            {
                SkipValue();
                b = SkipWhiteSpaces();
                if (b == ',')
                {
                    if (!buffer.TryNextByte()) throw new Exception("Failed reading array");
                    b = SkipWhiteSpaces();
                }
                else if (b != ']') throw new Exception("Failed reading array");
            }

            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading boolean");
        }

        private void SkipNull()
        {
            if (!TryReadNullValue()) throw new Exception("Failed reading null value");
        }

        private void SkipBool()
        {
            _ = ReadBoolValue();
        }

        private void SkipObject()
        {
            byte b = SkipWhiteSpaces();
            if (b != '{') throw new Exception("Failed reading object");
            buffer.TryNextByte();

            while (true)
            {
                b = SkipWhiteSpaces();
                if (b == '}') break;

                if (!TryReadStringBytes(out var fieldName)) throw new Exception("Failed reading object");
                b = SkipWhiteSpaces();
                if (b != ':') throw new Exception("Failed reading object");
                buffer.TryNextByte();
                SkipValue();
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();
            }

            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading object");
        }

        private void SkipString()
        {
            if (!TryReadStringBytes(out var _)) throw new Exception("Failed reading string");
        }

        double ApplyExponent(double value, int exponent)
        {
            int maxExponentFactorLookup = exponentFactorMap.Length-1;
            if (exponent < 0)
            {
                exponent = -exponent;

                if (exponent <= maxExponentFactorLookup)
                {
                    ulong factor = exponentFactorMap[exponent];
                    value = value / factor;
                    return value;
                }

                while (exponent > 0)
                {
                    int partialExp = exponent;
                    if (exponent > maxExponentFactorLookup)
                    {
                        partialExp = maxExponentFactorLookup;
                        exponent -= maxExponentFactorLookup;
                    }
                    else exponent = 0;

                    ulong factor = exponentFactorMap[partialExp];
                    value = value / factor;
                }
                return value;
            }
            else
            {
                if (exponent <= maxExponentFactorLookup)
                {
                    ulong factor = exponentFactorMap[exponent];
                    value = value * factor;
                    return value;
                }

                while (exponent > 0)
                {
                    int partialExp = exponent;
                    if (exponent > maxExponentFactorLookup)
                    {
                        partialExp = maxExponentFactorLookup;
                        exponent -= maxExponentFactorLookup;
                    }
                    else exponent = 0;

                    ulong factor = exponentFactorMap[partialExp];
                    value = value * factor;
                }
                return value;
            }                   
        }

        ulong ApplyExponent(ulong value, int exponent)
        {
            int maxExponentFactorLookup = exponentFactorMap.Length - 1;
            if (exponent < 0)
            {
                exponent = -exponent;

                if (exponent <= maxExponentFactorLookup)
                {
                    ulong factor = exponentFactorMap[exponent];
                    value = value / factor;
                    return value;
                }

                while (exponent > 0)
                {
                    int partialExp = exponent;
                    if (exponent > maxExponentFactorLookup)
                    {
                        partialExp = maxExponentFactorLookup;
                        exponent -= maxExponentFactorLookup;
                    }
                    else exponent = 0;

                    ulong factor = exponentFactorMap[partialExp];
                    value = value / factor;
                }
                return value;
            }
            else
            {
                if (exponent <= maxExponentFactorLookup)
                {
                    ulong factor = exponentFactorMap[exponent];
                    value = value * factor;
                    return value;
                }

                while (exponent > 0)
                {
                    int partialExp = exponent;
                    if (exponent > maxExponentFactorLookup)
                    {
                        partialExp = maxExponentFactorLookup;
                        exponent -= maxExponentFactorLookup;
                    }
                    else exponent = 0;

                    ulong factor = exponentFactorMap[partialExp];
                    value = value * factor;
                }
                return value;
            }
        }

        ulong BytesToInteger(ArraySegment<byte> bytes)
        {
            ulong value = 0;
            if (bytes.Count == 0) return value;
            value += (byte)(bytes.Get(0) - (byte)'0');
            for (int i = 1; i < bytes.Count; i++)
            {
                value *= 10;
                value += (byte)(bytes.Get(i) - (byte)'0');
            }
            return value;
        }

        [Flags]
        enum ValidNumberComponents
        {
            negativeSign        = 1 << 0,
            decimalPart         = 1 << 1,
            exponent            = 1 << 2,
            all                 = negativeSign | decimalPart | exponent,
            floatingPointNumber = negativeSign | decimalPart | exponent,
            signedInteger       = negativeSign | exponent,
            unsignedInteger     = exponent,
        }

        static readonly ByteSegment zeroAsBytes = new byte[] { (byte)'0' };
        bool TryReadNumberBytes(out bool isNegative, out ByteSegment integerBytes, out ByteSegment decimalBytes, out ByteSegment exponentBytes, out bool isExponentNegative, ValidNumberComponents validComponents)
        {
            bool stringAsNumberStarted = false;

            using(var undoHandle = CreateUndoReadHandle())
            {
                integerBytes = default;
                decimalBytes = default;
                exponentBytes = default;
                isNegative = false;
                isExponentNegative = false;

                // Skip whitespaces until number starts
                ref var map_SkipWhitespacesUntilNumberStarts_ref = ref map_SkipWhitespacesUntilNumberStarts[0];
                while (true)
                {
                    byte b = buffer.CurrentByte;
                    var result = Lookup(ref map_SkipWhitespacesUntilNumberStarts_ref, b);
                    if (result == FilterResult.Found) break;
                    else if (result == FilterResult.Skip) 
                    { 
                        if (!buffer.TryNextByte()) return false; 
                    }
                    else if (result == FilterResult.Unexpected)
                    {
                        if (b == '"' && !settings.strict && !stringAsNumberStarted)
                        {
                            stringAsNumberStarted = true;
                            if (!buffer.TryNextByte()) return false;
                            if (buffer.CurrentByte == '"')
                            {                                
                                if (buffer.TryNextByte() && map_IsFieldEnd[buffer.CurrentByte] == FilterResult.Unexpected) return false;

                                integerBytes = zeroAsBytes;
                                undoHandle.SetUndoReading(false);
                                return true;
                            }
                        }
                        else return false;
                    }
                }

                // Check if negative
                isNegative = buffer.CurrentByte == '-';
                if (isNegative)
                {
                    if (!validComponents.IsFlagSet(ValidNumberComponents.negativeSign)) return false;
                    if (!buffer.TryNextByte()) return false;
                }                

                var recording = buffer.StartRecording();

                bool couldNotSkip = false;
                // Read integer part
                ref var map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds_ref = ref map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds[0];
                while (true)
                {
                    byte b = buffer.CurrentByte;
                    FilterResult result = Lookup(ref map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds_ref, b);
                    if (result == FilterResult.Skip)
                    {
                        if (!buffer.TryNextByte())
                        {
                            couldNotSkip = true;
                            break;
                        }
                    }
                    else if (result == FilterResult.Found) break;
                    else if (result == FilterResult.Unexpected)
                    {
                        if (b == '"' && stringAsNumberStarted) break;
                        else return false;
                    }
                }

                integerBytes = recording.GetRecordedBytes(couldNotSkip);
                if (integerBytes.Count == 0 && !settings.strict) integerBytes = zeroAsBytes;

                if (buffer.CurrentByte == '.')
                {
                    if (!validComponents.IsFlagSet(ValidNumberComponents.decimalPart) && settings.strict) return false;

                    buffer.TryNextByte();
                    // Read decimal part
                    recording = buffer.StartRecording();
                    ref var map_SkipFiguresUntilExponentOrNumberEnds_ref = ref map_SkipFiguresUntilExponentOrNumberEnds[0];
                    while (true)
                    {
                        byte b = buffer.CurrentByte;
                        FilterResult result = Lookup(ref map_SkipFiguresUntilExponentOrNumberEnds_ref, b);
                        if (result == FilterResult.Skip)
                        {
                            if (!buffer.TryNextByte())
                            {
                                couldNotSkip = true;
                                break;
                            }
                        }
                        else if (result == FilterResult.Found) break;
                        else if (result == FilterResult.Unexpected)
                        {
                            if (b == '"' && stringAsNumberStarted) break;
                            else return false;
                        }
                    }
                    decimalBytes = recording.GetRecordedBytes(couldNotSkip);
                }

                if (buffer.CurrentByte == 'e' || buffer.CurrentByte == 'E')
                {
                    if (!validComponents.IsFlagSet(ValidNumberComponents.exponent)) return false;

                    buffer.TryNextByte();
                    // Read exponent part
                    isExponentNegative = buffer.CurrentByte == '-';
                    if (isExponentNegative || buffer.CurrentByte == '+') buffer.TryNextByte();
                    recording = buffer.StartRecording();
                    ref var map_SkipFiguresUntilNumberEnds_ref = ref map_SkipFiguresUntilNumberEnds[0];
                    while (true)
                    {
                        byte b = buffer.CurrentByte;
                        FilterResult result = Lookup(ref map_SkipFiguresUntilNumberEnds_ref, b);
                        if (result == FilterResult.Skip)
                        {
                            if (!buffer.TryNextByte())
                            {
                                couldNotSkip = true;
                                break;
                            }
                        }
                        else if (result == FilterResult.Found) break;
                        else if (result == FilterResult.Unexpected)
                        {
                            if (b == '"' && stringAsNumberStarted) break;
                            else return false;
                        }
                    }
                    exponentBytes = recording.GetRecordedBytes(couldNotSkip);
                }

                if (stringAsNumberStarted)
                {
                    if (buffer.CurrentByte == '"')
                    {
                        if (buffer.TryNextByte() && map_IsFieldEnd[buffer.CurrentByte] == FilterResult.Unexpected) return false;
                    }
                    else return false;
                }

                undoHandle.SetUndoReading(false);
                return true;
            }
        }

        bool TryReadStringBytes(out ByteSegment stringBytes)
        {
            using (var undoHandle = CreateUndoReadHandle())
            {
                stringBytes = default;

                // Skip whitespaces until string starts
                byte b = SkipWhiteSpaces();
                if (b != (byte)'"') return false;

                var recording = buffer.StartRecording(true);

                // Skip chars until string ends
                ref var map_SkipCharsUntilStringEndsOrMultiByteChar_ref = ref map_SkipCharsUntilStringEndsOrMultiByteChar[0];
                while (buffer.TryNextByte())
                {
                    b = buffer.CurrentByte;
                    FilterResult result = Lookup(ref map_SkipCharsUntilStringEndsOrMultiByteChar_ref, b);                    
                    if (result == FilterResult.Skip) continue;
                    else if (result == FilterResult.Found)
                    {
                        if (b == (byte)'"')
                        {
                            stringBytes = recording.GetRecordedBytes(false);
                            buffer.TryNextByte();
                            undoHandle.SetUndoReading(false);
                            return true;
                        }
                        else HandleSpecialChars(b);
                    }
                    else return false;
                }
                return false;
            }

            void HandleSpecialChars(byte b)
            {
                if (b == (byte)'\\')
                {
                    buffer.TryNextByte();
                }
                else if ((b & 0b11100000) == 0b11000000) // skip 1 byte
                {
                    buffer.TryNextByte();
                }
                else if ((b & 0b11110000) == 0b11100000) // skip 2 bytes
                {
                    buffer.TryNextByte();
                    buffer.TryNextByte();
                }
                else if ((b & 0b11111000) == 0b11110000) // skip 3 bytes
                {
                    buffer.TryNextByte();
                    buffer.TryNextByte();
                    buffer.TryNextByte();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FilterResult Lookup(FilterResult[] map, byte index)
        {
            Debug.Assert(map != null && map.Length > byte.MaxValue);
            return map[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FilterResult Lookup(ref FilterResult map_firstElement, byte index)
        {
            return Unsafe.Add(ref map_firstElement, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeResult Lookup(TypeResult[] map, byte index)
        {
            Debug.Assert(map != null && map.Length > byte.MaxValue);
            return map[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeResult Lookup(ref TypeResult map_firstElement, byte index)
        {
            return Unsafe.Add(ref map_firstElement, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LookupCheck(FilterResult[] map, byte index, FilterResult comparant)
        {
            return comparant == map[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LookupCheck(ref FilterResult map_firstElement, byte index, FilterResult comparant)
        {
            return comparant == Unsafe.Add(ref map_firstElement, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        byte SkipWhiteSpaces()
        {
            ref FilterResult map_SkipWhitespaces_ref = ref map_SkipWhitespaces[0];
            byte b = buffer.CurrentByte;
            if (LookupCheck(ref map_SkipWhitespaces_ref, b, FilterResult.Found)) return b;

            while (buffer.TryNextByte())
            {
                b = buffer.CurrentByte;
                if (LookupCheck(ref map_SkipWhitespaces_ref, b, FilterResult.Found)) return b;
            } 
            return b;
        }

        readonly static ByteSegment typeFieldName = "$type".ToByteArray();
        readonly static ByteSegment valueFieldName = "$value".ToByteArray();

        bool TryReadAsProposedType<T>(CachedTypeReader originalTypeReader, out T item)
        {
            item = default;
            byte b = SkipWhiteSpaces();
            if (b != (byte)'{') return false;

            CachedTypeReader proposedTypeReader = null;
            bool foundValueField = false;
            using (var undoHandle = CreateUndoReadHandle())
            {
                if (!FindProposedType(out proposedTypeReader, typeof(T), out foundValueField)) return false;
                undoHandle.SetUndoReading(!foundValueField);
            }

            if (foundValueField)
            {                
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (proposedTypeReader != null) item = proposedTypeReader.ReadItemIgnoreProposedType<T>();
                else item = originalTypeReader.ReadItem<T>();
                if (!TrySkipRemainingFieldsOfObject()) throw new Exception("Failed on SkipRemainingFieldsOfObject");
                return true;
            }
            else
            {
                // we need to reset the bufferpos, because the $type field was embedded in the actual value's object, so we read the object again from the start.                
                if (proposedTypeReader == null) return false;
                item = proposedTypeReader.ReadItemIgnoreProposedType<T>();
                return true;
            }            
        }

        bool TryReadAsProposedType<T>(CachedTypeReader originalTypeReader, T itemToPopulate, out T item)
        {
            item = default;
            byte b = SkipWhiteSpaces();
            if (b != (byte)'{') return false;

            CachedTypeReader proposedTypeReader = null;
            bool foundValueField = false;
            using (var undoHandle = CreateUndoReadHandle())
            {
                if (!FindProposedType(out proposedTypeReader, typeof(T), out foundValueField)) return false;
                undoHandle.SetUndoReading(!foundValueField);
            }

            if (foundValueField)
            {
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (proposedTypeReader != null)
                {                    
                    if (itemToPopulate.GetType().IsAssignableTo(proposedTypeReader.ReaderType)) item = proposedTypeReader.ReadItemIgnoreProposedType<T>(itemToPopulate);
                    else item = proposedTypeReader.ReadItemIgnoreProposedType<T>();
                }
                else item = originalTypeReader.ReadItem<T>(itemToPopulate);
                if (!TrySkipRemainingFieldsOfObject()) throw new Exception("Failed on SkipRemainingFieldsOfObject");
                return true;
            }
            else
            {
                // we need to reset the bufferpos, because the $type field was embedded in the actual value's object, so we read the object again from the start.                
                if (proposedTypeReader == null) return false;
                if (itemToPopulate.GetType().IsAssignableTo(proposedTypeReader.ReaderType)) item = proposedTypeReader.ReadItemIgnoreProposedType<T>(itemToPopulate);
                else item = proposedTypeReader.ReadItemIgnoreProposedType<T>();
                return true;
            }
        }

        bool FindProposedType(out CachedTypeReader proposedTypeReader, Type expectedType, out bool foundValueField)
        {
            proposedTypeReader = null;
            foundValueField = false;

            // IMPORTANT: Currently, the type-field must be the first, TODO: add option to allow it to be anywhere in the object (which is much slower)

            // 1. find $type field
            byte b = SkipWhiteSpaces();
            if (b != (byte)'{') return false;
            buffer.TryNextByte();
            // TODO compare byte per byte to fail early
            if (!TryReadStringBytes(out var fieldName)) return false;
            if (!typeFieldName.Equals(fieldName)) return false;
            b = SkipWhiteSpaces();
            if (b != (byte)':') return false;
            buffer.TryNextByte();
            if (!TryReadStringBytes(out var proposedTypeBytes)) return false;

            // 2. get proposedTypeReader, if possible
            string proposedTypename = Encoding.UTF8.GetString(proposedTypeBytes.AsArraySegment.Array, proposedTypeBytes.AsArraySegment.Offset, proposedTypeBytes.AsArraySegment.Count);
            Type proposedType = TypeNameHelper.GetTypeFromSimplifiedName(proposedTypename);
            if (proposedType != null && proposedType != expectedType && proposedType.IsAssignableTo(expectedType)) proposedTypeReader = GetCachedTypeReader(proposedType);

            // 3. look if next is $value field
            b = SkipWhiteSpaces();
            if (b != ',') return true;
            buffer.TryNextByte();
            // TODO compare byte per byte to fail early
            if (!TryReadStringBytes(out fieldName)) return true;
            if (!valueFieldName.Equals(fieldName)) return true;
            b = SkipWhiteSpaces();
            if (b != (byte)':') return true;
            buffer.TryNextByte();

            // 4. $value field found
            foundValueField = true;
            return true;
        }

        private bool TrySkipRemainingFieldsOfObject()
        {
            byte b = SkipWhiteSpaces();
            if (b == ',') buffer.TryNextByte();
            while (true)
            {
                b = SkipWhiteSpaces();
                if (b == '}') break;

                if (!TryReadStringBytes(out var _)) return false;
                b = SkipWhiteSpaces();
                if (b != ':') return false;
                buffer.TryNextByte();
                SkipValue();
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();
            }
            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;
            return true;
        }

        ByteSegment refFieldName = "$ref".ToByteArray();
        List<ByteSegment> fieldPathSegments = new List<ByteSegment>();

        bool TryReadRefObject<T>(out bool pathIsValid, out bool typeIsCompatible, out T refObject)
        {
            using (var undoHandle = CreateUndoReadHandle())
            {
                bool success = Try(out pathIsValid, out typeIsCompatible, out refObject);                
                undoHandle.SetUndoReading(!success);
                fieldPathSegments.Clear();
                return success;
            }

            bool Try(out bool pathIsValid, out bool typeIsCompatible, out T itemRef)
            {
                pathIsValid = false;
                typeIsCompatible = false;
                itemRef = default;

                // IMPORTANT: Currently, the ref-field must be the first, TODO: add option to allow it to be anywhere in the object (which is much slower)
                byte b = SkipWhiteSpaces();
                if (b != (byte)'{') return false;
                buffer.TryNextByte();
                // TODO compare byte per byte to fail early
                if (!TryReadStringBytes(out var fieldName)) return false;
                if (!refFieldName.Equals(fieldName)) return false;
                b = SkipWhiteSpaces();
                if (b != (byte)':') return false;
                buffer.TryNextByte();
                if (!TryReadStringBytes(out var refPath)) return false;
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();

                // Skip the rest
                while (true)
                {
                    b = SkipWhiteSpaces();
                    if (b == '}') break;

                    if (!TryReadStringBytes(out var _)) return false;
                    b = SkipWhiteSpaces();
                    if (b != ':') return false;
                    buffer.TryNextByte();
                    SkipValue();
                    b = SkipWhiteSpaces();
                    if (b == ',') buffer.TryNextByte();
                }
                if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;



                // TODO find object
                if (refPath.Count <= 0) return false;
                int pos = 0;
                int startPos = 0;
                int segmentLength = 0;
                int refPathCount = refPath.Count;
                b = refPath.AsArraySegment.Get(pos);

                while (true)
                {
                    if (b == '[')
                    {
                        while (true)
                        {
                            pos++;
                            if (pos >= refPathCount) return false;
                            b = refPath.AsArraySegment.Get(pos);
                            if (b == ']')
                            {
                                segmentLength = pos - startPos + 1;
                                pos++;
                                break;
                            }
                        }
                        ByteSegment segment = refPath.AsArraySegment.Slice(startPos, segmentLength);
                        fieldPathSegments.Add(segment);
                        if (pos >= refPathCount) break;
                    }
                    else
                    {
                        while (true)
                        {
                            pos++;
                            if (pos >= refPathCount)
                            {
                                segmentLength = pos - startPos;
                                break;
                            }
                            b = refPath.AsArraySegment.Get(pos);
                            if (b == '.')
                            {
                                segmentLength = pos - startPos;
                                pos++;
                                break;
                            }
                            if (b == '[')
                            {
                                segmentLength = pos - startPos;
                                break;
                            }

                        }
                        ByteSegment segment = refPath.AsArraySegment.Slice(startPos, segmentLength);
                        fieldPathSegments.Add(segment);
                        if (pos >= refPathCount) break;
                    }
                    startPos = pos;
                    b = refPath.AsArraySegment.Get(pos);
                }


                object potentialItemRef = null;
                int lastSegmentIndex = fieldPathSegments.Count - 1;
                var referencedFieldName = fieldPathSegments[lastSegmentIndex];
                foreach (var info in itemInfos)
                {
                    if (info.name.Equals(referencedFieldName))
                    {
                        potentialItemRef = info.itemRef;
                        int segmentIndex = lastSegmentIndex - 1;
                        int parentIndex = info.parentIndex;
                        ItemInfo parentInfo;
                        while (segmentIndex != -1 && parentIndex != -1)
                        {
                            var segment = fieldPathSegments[segmentIndex];
                            parentInfo = itemInfos[parentIndex];
                            if (!parentInfo.name.Equals(segment)) break;
                            parentIndex = parentInfo.parentIndex;
                            segmentIndex--;
                        }

                        pathIsValid = parentIndex == -1 && segmentIndex == -1;
                        if (pathIsValid) break;
                    }
                }

                if (pathIsValid && potentialItemRef is T compatibleItemRef)
                {
                    typeIsCompatible = true;
                    itemRef = compatibleItemRef;
                }

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryReadEmptyStringValue()
        {
            using (var undoHandle = CreateUndoReadHandle())
            {
                byte b = SkipWhiteSpaces();

                if (b != '\"') return false;
                if (!buffer.TryNextByte()) return false;
                b = buffer.CurrentByte;
                if (b != '\"') return false;
                // Check for field end
                if (!buffer.TryNextByte())
                {
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                if (!LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;

                undoHandle.SetUndoReading(false);
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryReadNullValue()
        {            
            using(var undoHandle = CreateUndoReadHandle())
            {
                byte b = SkipWhiteSpaces();

                if (b != 'n' && b != 'N') return false;
                if (!buffer.TryNextByte()) return false;
                b = buffer.CurrentByte;
                if (b != 'u' && b != 'U') return false;
                if (!buffer.TryNextByte()) return false;
                b = buffer.CurrentByte;
                if (b != 'l' && b != 'L') return false;
                if (!buffer.TryNextByte()) return false;
                b = buffer.CurrentByte;
                if (b != 'l' && b != 'L') return false;

                // Check for field end
                if (!buffer.TryNextByte())
                {
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                if (!LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;

                undoHandle.SetUndoReading(false);
                return true;
            }           
        }


        enum FilterResult
        {
            Skip,
            Found,
            Unexpected
        }

        public enum TypeResult
        {
            Whitespace,
            Object,
            Number,
            String,
            Null,
            Bool,
            Array,
            Invalid
        }

        static FilterResult[] CreateFilterMap_SkipWhitespacesUntilObjectStarts()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;
                else if (i == '{') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipWhitespacesUntilBoolStarts()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;
                else if (i == 't' || i == 'T' || i == 'f' || i == 'T') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipBoolCharsUntilEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if ("trueTRUEfalseFALSE".Contains((char)i)) map[i] = FilterResult.Skip;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipCharsUntilStringEndsOrMultiByteChar()
        {
            FilterResult[] map = new FilterResult[256];
            for (int b = 0; b < map.Length; b++)
            {
                if (b == '\"') map[b] = FilterResult.Found;
                else if (b == '\\') map[b] = FilterResult.Found;
                else if ((b & 0b11100000) == 0b11000000) map[b] = FilterResult.Found;
                else if ((b & 0b11110000) == 0b11100000) map[b] = FilterResult.Found;
                else if ((b & 0b11111000) == 0b11110000) map[b] = FilterResult.Found;
                else if ((b & 0b10000000) == 0b00000000) map[b] = FilterResult.Skip;
                else map[b] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipWhitespacesUntilNumberStarts()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;
                else if (i >= '0' && i <= '9') map[i] = FilterResult.Found;
                else if (i >= '-') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i >= '0' && i <= '9') map[i] = FilterResult.Skip;
                else if (i == '.') map[i] = FilterResult.Found;
                else if (i == 'e' || i == 'E') map[i] = FilterResult.Found;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipFiguresUntilExponentOrNumberEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i >= '0' && i <= '9') map[i] = FilterResult.Skip;
                else if (i == 'e' || i == 'E') map[i] = FilterResult.Found;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipFiguresUntilNumberEnds()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i >= '0' && i <= '9') map[i] = FilterResult.Skip;
                else if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_SkipWhitespaces()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;
                else map[i] = FilterResult.Found;
            }
            return map;
        }

        static FilterResult[] CreateFilterMap_IsFieldEnd()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
                else if (i == ',' || i == ']' || i == '}') map[i] = FilterResult.Found;
                else map[i] = FilterResult.Unexpected;
            }
            return map;
        }

        static TypeResult[] CreateTypeStartMap()
        {
            TypeResult[] map = new TypeResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = TypeResult.Whitespace;
                else if ((i >= '0' && i <= '9') || i == '-') map[i] = TypeResult.Number;
                else if (i == '\"') map[i] = TypeResult.String;
                else if (i == 'N' || i == 'n') map[i] = TypeResult.Null;
                else if (i == 'T' || i == 't' || i == 'F' || i == 'f') map[i] = TypeResult.Bool;
                else if (i == '{') map[i] = TypeResult.Object;
                else if (i == '[') map[i] = TypeResult.Array;
                else map[i] = TypeResult.Invalid;
            }
            return map;
        }
    }
}
