﻿using FeatureLoom.Synchronization;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Collections;
using System.Runtime.CompilerServices;
using FeatureLoom.Helpers;
using System.Text.Unicode;
using System.Linq.Expressions;
using Microsoft.VisualBasic.FileIO;
using FeatureLoom.DependencyInversion;
using Newtonsoft.Json.Linq;
using FeatureLoom.Collections;

namespace Playground
{
    public sealed partial class FeatureJsonDeserializer
    {
        MicroValueLock serializerLock = new MicroValueLock();
        const int BUFFER_SIZE = 1024 * 64;
        byte[] buffer = new byte[BUFFER_SIZE];
        int bufferPos = 0;
        int bufferFillLevel = 0;
        int bufferResetLevel = BUFFER_SIZE - (1024 * 8);
        long totalBytesRead = 0;
        Stream stream;

        Dictionary<Type, CachedTypeReader> typeReaderCache = new();
        Dictionary<Type, object> typeConstructorMap = new();

        static readonly FilterResult[] map_SkipWhitespaces = CreateFilterMap_SkipWhitespaces();
        static readonly FilterResult[] map_IsFieldEnd = CreateFilterMap_IsFieldEnd();
        static readonly FilterResult[] map_SkipWhitespacesUntilStringStarts = CreateFilterMap_SkipWhitespacesUntilStringStarts();
        static readonly FilterResult[] map_SkipCharsUntilStringEndsOrMultiByteChar = CreateFilterMap_SkipCharsUntilStringEndsOrMultiByteChar();
        static readonly FilterResult[] map_SkipWhitespacesUntilNumberStarts = CreateFilterMap_SkipWhitespacesUntilNumberStarts();
        static readonly FilterResult[] map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds = CreateFilterMap_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds();
        static readonly FilterResult[] map_SkipFiguresUntilExponentOrNumberEnds = CreateFilterMap_SkipFiguresUntilExponentOrNumberEnds();
        static readonly FilterResult[] map_SkipFiguresUntilNumberEnds = CreateFilterMap_SkipFiguresUntilNumberEnds();
        static readonly FilterResult[] map_SkipWhitespacesUntilObjectStarts = CreateFilterMap_SkipWhitespacesUntilObjectStarts();
        static readonly TypeResult[] map_TypeStart = CreateTypeStartMap();

        static ulong[] exponentFactorMap = CreateExponentFactorMap();

        static ulong[] CreateExponentFactorMap()
        {
            ulong[] map = new ulong[21];
            ulong factor = 1;
            map[0] = factor;
            for (int i = 1; i < map.Length; i++)
            {
                factor *= 10;
                map[i] = factor;
            }
            return map;
        }

        public class Settings
        {
            public DataAccess dataAccess = DataAccess.PublicAndPrivateFields;
            public Dictionary<Type, object> constructors = new();
            public Dictionary<Type, Type> typeMapping = new();
            public Dictionary<Type, Type> genericTypeMapping = new();
            public void AddConstructor<T>(Func<T> constructor) => constructors[typeof(T)] = constructor;
            public void AddTypeMapping<BASE_T, IMPL_T>() where IMPL_T : BASE_T => typeMapping[typeof(BASE_T)] = typeof(IMPL_T);            
            public void AddGenericTypeMapping(Type genericBaseType, Type genericImplType)
            {
                if (!genericImplType.IsOfGenericType(genericBaseType)) throw new Exception($"{TypeNameHelper.GetSimplifiedTypeName(genericBaseType)} is not implemented by {TypeNameHelper.GetSimplifiedTypeName(genericImplType)}");
                genericTypeMapping[genericBaseType] = genericImplType;
            }
            
        }

        public enum DataAccess
        {
            PublicAndPrivateFields = 0,
            PublicFieldsAndProperties = 1
        }

        Settings settings;

        public FeatureJsonDeserializer(Settings settings = null)
        {                        
            this.settings = settings ?? new Settings();
        }

        CachedTypeReader GetCachedTypeReader(Type itemType)
        {
            if (typeReaderCache.TryGetValue(itemType, out var cachedTypeReader)) return cachedTypeReader;
            else return CreateCachedTypeReader(itemType);
        }

        CachedTypeReader CreateCachedTypeReader(Type itemType)
        {
            if (settings.typeMapping.TryGetValue(itemType, out Type mappedType))
            {
                CachedTypeReader mappedTypeReader = GetCachedTypeReader(mappedType);
                typeReaderCache[itemType] = mappedTypeReader;
                return mappedTypeReader;
            }
            if (itemType.IsGenericType && settings.genericTypeMapping.Count > 0)
            {
                Type genericType = itemType.GetGenericTypeDefinition();                
                if (settings.genericTypeMapping.TryGetValue(genericType, out Type genericMappedType))
                {

                }
            }

            CachedTypeReader cachedTypeReader = new CachedTypeReader(this);
            typeReaderCache[itemType] = cachedTypeReader;

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
            else if (itemType == typeof(bool)) cachedTypeReader.SetTypeReader(ReadBoolValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(bool?)) cachedTypeReader.SetTypeReader(ReadNullableBoolValue, JsonDataTypeCategory.Primitive);
            else if (itemType == typeof(object)) cachedTypeReader.SetTypeReader(ReadUnknownValue, JsonDataTypeCategory.Object);
            else if (itemType.IsEnum) this.InvokeGenericMethod(nameof(CreateEnumReader), new Type[] { itemType }, cachedTypeReader);
            else if (TryCreateDictionaryTypeReader(itemType, cachedTypeReader)) { }
            else if (TryCreateEnumerableTypeReader(itemType, cachedTypeReader)) { }
            else this.InvokeGenericMethod(nameof(CreateComplexTypeReader), new Type[] { itemType }, cachedTypeReader);

            return cachedTypeReader;
        }

        private Func<T> GetConstructor<T>()
        {
            Type type = typeof(T);
            if (settings.constructors.TryGetValue(type, out object c) && c is Func<T> constructor) return constructor;
            if (null != type.GetConstructor(Array.Empty<Type>())) return CompileConstructor<T>();
            if (type.IsValueType) return () => default;

            throw new Exception($"No default constructor for type {TypeNameHelper.GetSimplifiedTypeName(type)}. Use AddConstructor in Settings.");
        }


        private static Func<T> CompileConstructor<T>()
        {
            Type type = typeof(T);
            var newExpr = Expression.New(type);
            var lambda = Expression.Lambda<Func<T>>(newExpr);
            return lambda.Compile();
        }

        private static Func<P, T> CompileConstructor<T, P>()
        {
            var param = Expression.Parameter(typeof(P), "value");
            var newExpr = Expression.New(typeof(T).GetConstructor(new[] { typeof(P) }), param);
            var lambda = Expression.Lambda<Func<P, T>>(newExpr, param);
            return lambda.Compile();
        }

        private void CreateEnumReader<T>(CachedTypeReader cachedTypeReader) where T: struct, Enum
        {
            cachedTypeReader.SetTypeReader(() =>
            {
                var valueType = map_TypeStart[CurrentByte];
                if (valueType == TypeResult.Whitespace)
                {
                    SkipWhiteSpaces();
                    valueType = map_TypeStart[CurrentByte];
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
            var valueType = map_TypeStart[CurrentByte];
            if (valueType == TypeResult.Whitespace)
            {
                SkipWhiteSpaces();
                valueType = map_TypeStart[CurrentByte];
            }

            switch (valueType)
            {
                case TypeResult.String: return ReadStringValue();
                case TypeResult.Object: return ReadObjectValueAsDictionary();
                case TypeResult.Bool: return ReadBoolValue();
                case TypeResult.Null: return ReadNullValue();
                case TypeResult.Array: return ReadArrayValueAsObject();
                case TypeResult.Number: return ReadNumberValueAsObject();
                default: throw new Exception("Invalid character for determining value");
            }
        }

        CachedTypeReader cachedStringObjectDictionaryReader = null;

        private Dictionary<string, object> ReadObjectValueAsDictionary()
        {
            if (cachedStringObjectDictionaryReader == null) cachedStringObjectDictionaryReader = CreateCachedTypeReader(typeof(Dictionary<string, object>));
            return cachedStringObjectDictionaryReader.ReadItem<Dictionary<string, object>>();
        }

        private bool TryCreateDictionaryTypeReader(Type itemType, CachedTypeReader cachedTypeReader)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type keyType, out Type valueType)) return false;
            if (itemType.IsInterface) throw new NotImplementedException();  //TODO
            var dictionaryType = typeof(IDictionary<,>).MakeGenericType(keyType, valueType);

            this.InvokeGenericMethod(nameof(CreateDictionaryTypeReader), new Type[] { itemType, keyType, valueType }, cachedTypeReader);

            return true;
        }

        private void CreateDictionaryTypeReader<T, K, V>(CachedTypeReader cachedTypeReader) where T : IDictionary<K, V>, new()
        {
            var constructor = CompileConstructor<T>();
            var elementReader = new ElementReader<KeyValuePair<K, V>>(this);
            var keyReader = GetCachedTypeReader(typeof(K));
            var valueReader = GetCachedTypeReader(typeof(V));

            cachedTypeReader.SetTypeReader(() =>
            {
                T dict = constructor();
                SkipWhiteSpaces();
                if (CurrentByte == '{')
                {
                    if (!TryNextByte()) throw new Exception("Failed reading Object to Dictionary");
                    while (true)
                    {
                        SkipWhiteSpaces();
                        if (CurrentByte == '}') break;

                        K fieldName = keyReader.ReadItem<K>();
                        SkipWhiteSpaces();
                        if (CurrentByte != ':') throw new Exception("Failed reading object to Dictionary");
                        TryNextByte();
                        V value = valueReader.ReadItem<V>();
                        dict[fieldName] = value;
                        SkipWhiteSpaces();
                        if (CurrentByte == ',') TryNextByte();
                    }

                    if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading object to Dictionary");
                }
                else if (CurrentByte == '[')
                {
                    if (!TryNextByte()) throw new Exception("Failed reading Array to Dictionary");
                    foreach(var element in elementReader)
                    {
                        dict.Add(element.Key, element.Value);
                    }
                    if (CurrentByte != ']') throw new Exception("Failed reading Array to Dictionary");
                    TryNextByte();
                }
                else throw new Exception("Failed reading Dictionary");

                return dict;
            }, JsonDataTypeCategory.Array);
        }

        private void CreateComplexTypeReader<T>(CachedTypeReader cachedTypeReader)
        {
            Type itemType = typeof(T);

            var memberInfos = new List<MemberInfo>();
            if (settings.dataAccess == DataAccess.PublicFieldsAndProperties)
            {
                memberInfos.AddRange(itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.SetMethod != null));
                memberInfos.AddRange(itemType.GetFields(BindingFlags.Public | BindingFlags.Instance));
            }
            else
            {
                memberInfos.AddRange(itemType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                Type t = itemType.BaseType;
                while (t != null)
                {
                    memberInfos.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !memberInfos.Any(field => field.Name == baseField.Name)));
                    t = t.BaseType;
                }
            }

            Dictionary<EquatableByteSegment, Func<T,T>> itemFieldWriters = new();
            foreach (var memberInfo in memberInfos)
            {
                Type fieldType = GetFieldOrPropertyType(memberInfo);
                
                var itemFieldWriter = this.InvokeGenericMethod<Func<T,T>>(nameof(CreateItemFieldWriter), new Type[] { itemType, fieldType }, memberInfo);
                string name = memberInfo.Name;
                if (name.TryExtract("<{name}>k__BackingField", out string backingFieldName)) name = backingFieldName;
                var itemFieldName = new EquatableByteSegment(name.ToByteArray());
                itemFieldWriters[itemFieldName] = itemFieldWriter;
            }

            var constructor = GetConstructor<T>();

            Func<T> typeReader = () =>
            {
                T item = constructor();

                SkipWhiteSpaces();
                if (CurrentByte != '{') throw new Exception("Failed reading object");
                TryNextByte();
                
                while (true)
                {
                    SkipWhiteSpaces();
                    if (CurrentByte == '}') break;

                    if (!TryReadStringBytes(out var fieldName)) throw new Exception("Failed reading object");
                    SkipWhiteSpaces();
                    if (CurrentByte != ':') throw new Exception("Failed reading object");
                    TryNextByte();
                    if (itemFieldWriters.TryGetValue(fieldName, out var fieldWriter)) item = fieldWriter.Invoke(item);
                    else SkipValue();
                    SkipWhiteSpaces();
                    if (CurrentByte == ',') TryNextByte();
                }

                if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading object");
                return item;
            };
            
            cachedTypeReader.SetTypeReader(typeReader, JsonDataTypeCategory.Object);
            
        }


        private Func<T,T> CreateItemFieldWriter<T, V>(MemberInfo memberInfo)
        {
            Type itemType = typeof(T);
            Type fieldType = typeof(V);

            if (memberInfo is FieldInfo fieldInfo)
            {
                if (fieldInfo.IsInitOnly) // Check if the field is read-only
                {
                    // Handle read-only fields
                    var fieldTypeReader = GetCachedTypeReader(fieldType);
                    if (itemType.IsValueType)
                    {
                        return parentItem =>
                        {
                            V value = fieldTypeReader.ReadItem<V>();
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
                            V value = fieldTypeReader.ReadItem<V>();
                            fieldInfo.SetValue(parentItem, value);
                            return parentItem;
                        };
                    }
                }
                else
                {
                    // Use expression tree for normal writable field
                    return CreateFieldWriterUsingExpression<T, V>(fieldInfo);
                }
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                if (propertyInfo.CanWrite)
                {
                    // Use expression tree for writable property
                    return CreatePropertyWriterUsingExpression<T, V>(propertyInfo);
                }
                else if (HasInitAccessor(propertyInfo))
                {
                    // Handle init-only properties
                    var fieldTypeReader = GetCachedTypeReader(fieldType);
                    if (itemType.IsValueType)
                    {
                        return parentItem =>
                        {
                            V value = fieldTypeReader.ReadItem<V>();
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
                            V value = fieldTypeReader.ReadItem<V>();
                            propertyInfo.SetValue(parentItem, value);
                            return parentItem;
                        };
                    }
                }
            }

            throw new InvalidOperationException("MemberInfo must be a writable field, property, or init-only property.");
        }

        private bool HasInitAccessor(PropertyInfo propertyInfo)
        {
            // Use reflection to check if the property has an init accessor
            var setMethod = propertyInfo.SetMethod;
            return setMethod != null && setMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
        }

        private Func<T,T> CreateFieldWriterUsingExpression<T, V>(FieldInfo fieldInfo)
        {
            Type itemType = typeof(T);
            Type fieldType = typeof(V);

            var target = Expression.Parameter(itemType, "target");
            var value = Expression.Parameter(fieldType, "value");
            MemberExpression memberAccess = Expression.Field(target, fieldInfo);
            BinaryExpression assignExpression = Expression.Assign(memberAccess, value);
            var lambda = Expression.Lambda<Action<T, V>>(assignExpression, target, value);
            Action<T, V> setValue = lambda.Compile();

            var fieldTypeReader = GetCachedTypeReader(fieldType);
            return parentItem =>
            {
                V value = fieldTypeReader.ReadItem<V>();
                setValue(parentItem, value);
                return parentItem;
            };
        }

        private Func<T,T> CreatePropertyWriterUsingExpression<T, V>(PropertyInfo propertyInfo)
        {
            Type itemType = typeof(T);
            Type fieldType = typeof(V);

            var target = Expression.Parameter(itemType, "target");
            var value = Expression.Parameter(fieldType, "value");
            MemberExpression memberAccess = Expression.Property(target, propertyInfo);
            BinaryExpression assignExpression = Expression.Assign(memberAccess, value);
            var lambda = Expression.Lambda<Action<T, V>>(assignExpression, target, value);
            Action<T, V> setValue = lambda.Compile();

            var fieldTypeReader = GetCachedTypeReader(fieldType);
            return parentItem =>
            {
                V value = fieldTypeReader.ReadItem<V>();
                setValue(parentItem, value);
                return parentItem;
            };
        }

        private Type GetFieldOrPropertyType(MemberInfo fieldOrPropertyInfo)
        {
            if (fieldOrPropertyInfo is FieldInfo fieldInfo) return fieldInfo.FieldType;
            else if (fieldOrPropertyInfo is PropertyInfo propertyInfo) return propertyInfo.PropertyType;
            throw new Exception("Not a FieldType or PropertyType");
        }

        private void CreateArrayTypeReader(Type arrayType, CachedTypeReader cachedTypeReader)
        {
            this.InvokeGenericMethod(nameof(CreateGenericArrayTypeReader), new Type[] { arrayType.GetElementType() }, cachedTypeReader);
        }

        private void CreateGenericArrayTypeReader<E>(CachedTypeReader cachedTypeReader)
        {
            var elementReader = new ElementReader<E>(this);
            Pool<List<E>> pool = new Pool<List<E>>(() => new List<E>(), l => l.Clear());
            cachedTypeReader.SetTypeReader(() =>
            {
                SkipWhiteSpaces();
                if (CurrentByte != '[') throw new Exception("Failed reading Array");
                if (!TryNextByte()) throw new Exception("Failed reading Array");
                List<E> elementBuffer = pool.Take();
                elementBuffer.AddRange(elementReader);
                E[] item = elementBuffer.ToArray();
                pool.Return(elementBuffer);
                if (CurrentByte != ']') throw new Exception("Failed reading Array");
                TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);

        }

        private bool TryCreateEnumerableTypeReader(Type itemType, CachedTypeReader cachedTypeReader)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType)) return false;
            if (itemType.IsInterface) throw new NotImplementedException();  //TODO
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);

            this.InvokeGenericMethod(nameof(CreateEnumerableTypeReader), new Type[] {itemType, elementType}, cachedTypeReader);

            return true;
        }

        private void CreateEnumerableTypeReader<T, E>(CachedTypeReader cachedTypeReader)
        {
            var elementReader = new ElementReader<E>(this);

            var constructor = CompileConstructor<T, IEnumerable<E>>();

            Pool<List<E>> pool = new Pool<List<E>>(() => new List<E>(), l => l.Clear());            
            cachedTypeReader.SetTypeReader<T>(() =>
            {
                SkipWhiteSpaces();
                if (CurrentByte != '[') throw new Exception("Failed reading Array");
                if (!TryNextByte()) throw new Exception("Failed reading Array");
                List<E> elementBuffer = pool.Take();
                elementBuffer.AddRange(elementReader);
                T item = constructor(elementBuffer);
                pool.Return(elementBuffer);
                if (CurrentByte != ']') throw new Exception("Failed reading Array");
                TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);
        }

        class ElementReader<T> : IEnumerable<T>, IEnumerator<T>
        {
            FeatureJsonDeserializer deserializer;
            CachedTypeReader reader;
            T current = default;

            public ElementReader(FeatureJsonDeserializer deserializer)
            {
                this.deserializer = deserializer;
                reader = deserializer.GetCachedTypeReader(typeof(T));
            }

            public T Current => current;

            object IEnumerator.Current => current;

            public bool MoveNext()
            {
                deserializer.SkipWhiteSpaces();
                if (deserializer.CurrentByte == ']') return false;                
                current = reader.ReadItem<T>();
                deserializer.SkipWhiteSpaces();
                if (deserializer.CurrentByte == ',') deserializer.TryNextByte();
                else if (deserializer.CurrentByte != ']') throw new Exception("Failed reading Array");
                return true;               
            }

            public void Reset()
            {
                current = default;
            }

            public void Dispose()
            {
                current = default;
            }

            public IEnumerator<T> GetEnumerator() => this;        

            IEnumerator IEnumerable.GetEnumerator() => this;
        }

        Pool<List<object>> objectBufferPool = new Pool<List<object>>(() => new List<object>(), l => l.Clear());


        public object[] ReadArrayValueAsObject()
        {                        
            if (CurrentByte != '[') throw new Exception("Failed reading Array");
            if (!TryNextByte()) throw new Exception("Failed reading Array");

            List<object> objectBuffer = objectBufferPool.Take();
            while (true)
            {
                SkipWhiteSpaces();
                if (CurrentByte == ']') break;
                object current = ReadUnknownValue();
                objectBuffer.Add(current);
                SkipWhiteSpaces();
                if (CurrentByte == ',') TryNextByte();
                else if (CurrentByte != ']') throw new Exception("Failed reading Array");
            }

            object[] objectArray = objectBuffer.Count > 0 ? objectBuffer.ToArray() : Array.Empty<object>();
            objectBufferPool.Return(objectBuffer);
            if (CurrentByte != ']') throw new Exception("Failed reading Array");
            TryNextByte();
            return objectArray;            
        }

        public string ReadStringValue()
        {
            if (!TryReadStringBytes(out var stringBytes)) throw new Exception("Failed reading string");
            //return Encoding.UTF8.GetString(stringBytes.Array, stringBytes.Offset, stringBytes.Count); //TODO: Implement UTF8 decoding using a StringBuilder. Escaped characters must be unescaped!
            return DecodeUtf8(stringBytes);
        }

        StringBuilder stringBuilder = new StringBuilder();

        private string DecodeUtf8(ArraySegment<byte> bytes)
        {
            int i = bytes.Offset;
            int end = bytes.Offset + bytes.Count;

            while (i < end)
            {
                byte b = buffer[i++];

                if (b == '\\' && i < end)
                {
                    b = buffer[i++];
                    
                    if (b == 'b') stringBuilder.Append('\b');
                    else if (b == 'f') stringBuilder.Append('\f');
                    else if (b == 'n') stringBuilder.Append('\n');
                    else if (b == 'r') stringBuilder.Append('\r');
                    else if (b == 't') stringBuilder.Append('\t');
                    else if (b == 'u')
                    {
                        if (i + 4 > end) stringBuilder.Append((char)b);
                        else
                        {
                            byte b1 = buffer[i++];
                            byte b2 = buffer[i++];
                            byte b3 = buffer[i++];
                            byte b4 = buffer[i++];
                            int codepoint = ((b1 & 0x07) << 18) | ((b2 & 0x3F) << 12) | ((b3 & 0x3F) << 6) | (b4 & 0x3F);
                            if (codepoint > 0xFFFF)
                            {
                                codepoint -= 0x10000;
                                stringBuilder.Append((char)(0xD800 | (codepoint >> 10)));
                                stringBuilder.Append((char)(0xDC00 | (codepoint & 0x3FF)));
                            }
                            else
                            {
                                stringBuilder.Append((char)codepoint);
                            }
                        }
                    }
                    else stringBuilder.Append((char)b);
                }
                else if (b < 0x80)
                {
                    stringBuilder.Append((char)b);
                }
                else if (b < 0xE0)
                {
                    byte b2 = buffer[i++];
                    stringBuilder.Append((char)(((b & 0x1F) << 6) | (b2 & 0x3F)));
                }
                else if (b < 0xF0)
                {
                    byte b2 = buffer[i++];
                    byte b3 = buffer[i++];
                    stringBuilder.Append((char)(((b & 0x0F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F)));
                }
                else
                {
                    byte b2 = buffer[i++];
                    byte b3 = buffer[i++];
                    byte b4 = buffer[i++];
                    int codepoint = ((b & 0x07) << 18) | ((b2 & 0x3F) << 12) | ((b3 & 0x3F) << 6) | (b4 & 0x3F);

                    if (codepoint > 0xFFFF)
                    {
                        codepoint -= 0x10000;
                        stringBuilder.Append((char)(0xD800 | (codepoint >> 10)));
                        stringBuilder.Append((char)(0xDC00 | (codepoint & 0x3FF)));
                    }
                    else
                    {
                        stringBuilder.Append((char)codepoint);
                    }
                }
            }

            string result =  stringBuilder.ToString();
            stringBuilder.Clear();
            return result;
        }

        public object ReadNullValue()
        {
            SkipWhiteSpaces();
            byte b = CurrentByte;
            if (b != 'n' && b != 'N') throw new Exception("Failed reading null");

            if (!TryNextByte()) throw new Exception("Failed reading null");
            b = CurrentByte;
            if (b != 'u' && b != 'U') throw new Exception("Failed reading null");

            if (!TryNextByte()) throw new Exception("Failed reading null");
            b = CurrentByte;
            if (b != 'l' && b != 'L') throw new Exception("Failed reading null");

            if (!TryNextByte()) throw new Exception("Failed reading null");
            b = CurrentByte;
            if (b != 'l' && b != 'L') throw new Exception("Failed reading null");

            if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading null");
            return null;
        }

        public bool ReadBoolValue()
        {
            SkipWhiteSpaces();
            byte b = CurrentByte;
            if (b == 't' || b == 'T')
            {
                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'r' && b != 'R') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'u' && b != 'U') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'e' && b != 'E') throw new Exception("Failed reading boolean");

                if(TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading boolean");
                return true;
            }
            else if (b == 'f' || b == 'F')
            {
                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'a' && b != 'A') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'l' && b != 'L') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 's' && b != 'S') throw new Exception("Failed reading boolean");

                if (!TryNextByte()) throw new Exception("Failed reading boolean");
                b = CurrentByte;
                if (b != 'e' && b != 'E') throw new Exception("Failed reading boolean");

                if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading boolean");
                return false;
            }
            else throw new Exception("Failed reading boolean");
        }

        public bool? ReadNullableBoolValue() => ReadBoolValue();

        public object ReadNumberValueAsObject()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");
            if (decimalBytes.Array != null || isExponentNegative)
            {
                ulong integerPart = integerBytes.EmptyOrNull() ? 0 : BytesToInteger(integerBytes);
                double decimalPart = decimalBytes.EmptyOrNull() ? 0 : BytesToInteger(decimalBytes);
                double value = decimalPart / exponentFactorMap[decimalBytes.Count];
                value += integerPart;
                if (isNegative) value *= -1;

                if (exponentBytes.Array != null)
                {
                    int exp = (int)BytesToInteger(exponentBytes);
                    int expFactor = (int)exponentFactorMap[exp];
                    if (isExponentNegative) value /= expFactor;
                    else value *= expFactor;
                }

                return value;
            }
            else
            {
                ulong integerPart = integerBytes.EmptyOrNull() ? 0 : BytesToInteger(integerBytes);
                if (exponentBytes.Array != null)
                {
                    int exp = (int)BytesToInteger(exponentBytes);
                    ulong expFactor = exponentFactorMap[exp];
                    integerPart *= expFactor;
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

        public long ReadLongValue()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");
            if (decimalBytes.Array != null) throw new Exception("Decimal found for integer");

            ulong integerPart = BytesToInteger(integerBytes);
            long value = (long)integerPart;
            if (isNegative) value *= -1;

            if (exponentBytes.Array != null)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                long expFactor = (long)exponentFactorMap[exp];
                if (isExponentNegative) value /= expFactor;
                else value *= expFactor;
            }

            return value;
        }
        public long? ReadNullableLongValue() => ReadLongValue();

        public int ReadIntValue()
        {
            long longValue = ReadLongValue();
            if (longValue > int.MaxValue || longValue < int.MinValue) throw new Exception("Value is out of bounds.");
            return (int)longValue;
        }
        public int? ReadNullableIntValue() => ReadIntValue();
        
        public short ReadShortValue()
        {
            long longValue = ReadLongValue();
            if (longValue > short.MaxValue || longValue < short.MinValue) throw new Exception("Value is out of bounds.");
            return (short)longValue;
        }
        public short? ReadNullableShortValue() => ReadShortValue();

        public sbyte ReadSbyteValue()
        {
            long longValue = ReadLongValue();
            if (longValue > sbyte.MaxValue || longValue < sbyte.MinValue) throw new Exception("Value is out of bounds.");
            return (sbyte)longValue;
        }
        public sbyte? ReadNullableSbyteValue() => ReadSbyteValue();

        public ulong ReadUlongValue()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");
            if (decimalBytes.Array != null) throw new Exception("Decimal found for integer");
            if (isNegative) throw new Exception("Value is out of bounds.");

            ulong value = BytesToInteger(integerBytes);

            if (exponentBytes.Array != null)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                ulong expFactor = exponentFactorMap[exp];
                if (isExponentNegative) value /= expFactor;
                else value *= expFactor;
            }

            return value;
        }
        public ulong? ReadNullableUlongValue() => ReadUlongValue();

        public uint ReadUintValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > uint.MaxValue) throw new Exception("Value is out of bounds.");
            return (uint)longValue;
        }
        public uint? ReadNullableUintValue() => ReadUintValue();

        public ushort ReadUshortValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > ushort.MaxValue) throw new Exception("Value is out of bounds.");
            return (ushort)longValue;
        }
        public ushort? ReadNullableUshortValue() => ReadUshortValue();

        public byte ReadByteValue()
        {
            ulong longValue = ReadUlongValue();
            if (longValue > byte.MaxValue) throw new Exception("Value is out of bounds.");
            return (byte)longValue;
        }
        public byte? ReadNullableByteValue() => ReadByteValue();

        public double ReadDoubleValue()
        {
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");
            

            ulong integerPart = integerBytes.EmptyOrNull() ? 0 : BytesToInteger(integerBytes);
            double decimalPart = decimalBytes.EmptyOrNull() ? 0 : BytesToInteger(decimalBytes);
            double value = decimalPart / exponentFactorMap[decimalBytes.Count];
            value += integerPart;
            if (isNegative) value *= -1;

            if (exponentBytes.Array != null)
            {
                int exp = (int)BytesToInteger(exponentBytes);
                int expFactor = (int)exponentFactorMap[exp];
                if (isExponentNegative) value /= expFactor;
                else value *= expFactor;
            }

            return value;
        }
        public double? ReadNullableDoubleValue() => ReadDoubleValue();

        public float ReadFloatValue() => (float)ReadDoubleValue();
        public float? ReadNullableFloatValue() => (float?)ReadDoubleValue();

        public bool TryDeserialize<T>(Stream stream, out T item, bool continueReading = true)
        {
            serializerLock.Enter();
            try
            {                
                this.stream = stream;
                /*if (!continueReading || bufferPos >= bufferFillLevel)
                {
                    bufferFillLevel = 0;
                    bufferPos = 0;
                }
                totalBytesRead = bufferFillLevel - bufferPos;
                */
                bufferFillLevel = stream.Read(buffer, 0, buffer.Length);
                var reader = GetCachedTypeReader(typeof(T));
                item = reader.ReadItem<T>();
                return true;
            }
            finally
            {
                serializerLock.Exit();
            }
        }

        private async Task<bool> TryReadToBuffer()
        {
            if (bufferFillLevel > bufferResetLevel)
            {
                bufferPos = 0;
                bufferFillLevel = 0;
            }
            int bytesRead = await stream.ReadAsync(buffer, bufferFillLevel, buffer.Length - bufferFillLevel);
            totalBytesRead += bytesRead;
            bufferFillLevel += bytesRead;
            return bufferFillLevel > bufferPos;
        }

        private bool TryNextByte()
        {
            if (++bufferPos < bufferFillLevel) return true;
            --bufferPos;
            return false;            
        }

        private byte CurrentByte => buffer[bufferPos];

        void SkipValue()
        {
            var valueType = map_TypeStart[CurrentByte];
            if (valueType == TypeResult.Whitespace)
            {
                SkipWhiteSpaces();
                valueType = map_TypeStart[CurrentByte];
            }

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
            if (!TryReadNumberBytes(out var isNegative, out var integerBytes, out var decimalBytes, out var exponentBytes, out bool isExponentNegative)) throw new Exception("Failed reading number");
        }

        private void SkipArray()
        {
            SkipWhiteSpaces();
            if (CurrentByte != '[') throw new Exception("Failed reading array");
            if (!TryNextByte()) throw new Exception("Failed reading array");
            SkipWhiteSpaces();
            while (CurrentByte != ']')
            {
                SkipValue();
                SkipWhiteSpaces();
                if (CurrentByte == ',')
                {
                    if (!TryNextByte()) throw new Exception("Failed reading array");
                    SkipWhiteSpaces();
                }
                else if (CurrentByte != ']') throw new Exception("Failed reading array");
            }

            if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading boolean");
        }

        private void SkipNull()
        {
            SkipWhiteSpaces();
            byte b;
            
            b = CurrentByte;
            if (b != 'N' && b != 'n') throw new Exception("Failed reading null value");
            
            if (!TryNextByte()) throw new Exception("Failed reading null value");            
            b = CurrentByte;
            if (b != 'U' && b != 'u') throw new Exception("Failed reading null value");
            
            if (!TryNextByte()) throw new Exception("Failed reading null value");
            b = CurrentByte;
            if (b != 'L' && b != 'l') throw new Exception("Failed reading null value");
            
            if (!TryNextByte()) throw new Exception("Failed reading null value");
            b = CurrentByte;
            if (b != 'L' && b != 'l') throw new Exception("Failed reading null value");
            
            if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading boolean");
        }

        private void SkipBool()
        {
            _ = ReadBoolValue();
        }

        private void SkipObject()
        {
            SkipWhiteSpaces();
            if (CurrentByte != '{') throw new Exception("Failed reading object");
            TryNextByte();

            while (true)
            {
                SkipWhiteSpaces();
                if (CurrentByte == '}') break;

                if (!TryReadStringBytes(out var fieldName)) throw new Exception("Failed reading object");
                SkipWhiteSpaces();
                if (CurrentByte != ':') throw new Exception("Failed reading object");
                TryNextByte();
                SkipValue();
                SkipWhiteSpaces();
                if (CurrentByte == ',') TryNextByte();
            }

            if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading boolean");
        }

        private void SkipString()
        {
            if (!TryReadStringBytes(out var _)) throw new Exception("Failed reading string");
        }

        ulong BytesToInteger(ArraySegment<byte> bytes)
        {
            ulong value = 0;
            if (bytes.Count == 0) return value;
            value += (byte)(bytes[0] - (byte)'0');
            for (int i = 1; i < bytes.Count; i++)
            {
                value *= 10;
                value += (byte)(bytes[i] - (byte)'0');                
            }
            return value;
        }

        bool TryReadNumberBytes(out bool isNegative, out ArraySegment<byte> integerBytes, out ArraySegment<byte> decimalBytes, out ArraySegment<byte> exponentBytes, out bool isExponentNegative)
        {
            integerBytes = default;
            decimalBytes = default;
            exponentBytes = default;
            isNegative = false;
            isExponentNegative = false;

            // Skip whitespaces until number starts
            while (true)
            {
                byte b = CurrentByte;
                var result = map_SkipWhitespacesUntilNumberStarts[b];
                if (result == FilterResult.Found) break;
                else if (result == FilterResult.Skip) { if (!TryNextByte()) return false; }                
                else if (result == FilterResult.Unexpected) return false;
            }
            
            // Check if negative
            isNegative = CurrentByte == '-';
            if (isNegative && !TryNextByte()) return false;
            int startPos = bufferPos;

            bool couldNotSkip = false;
            // Read integer part
            while (true)
            {
                byte b = CurrentByte;
                FilterResult result = map_SkipFiguresUntilDecimalPointOrExponentOrNumberEnds[b];
                if (result == FilterResult.Skip) 
                {
                    if (!TryNextByte())
                    {
                        couldNotSkip = true;
                        break;
                    }
                }
                else if (result == FilterResult.Found) break;
                else if (result == FilterResult.Unexpected) return false;
            }
            int count = bufferPos - startPos;
            if (couldNotSkip) count++;
            integerBytes = new ArraySegment<byte>(buffer, startPos, count);

            if (CurrentByte == '.')
            {                
                TryNextByte();
                // Read decimal part
                startPos = bufferPos;
                while (true)
                {
                    byte b = CurrentByte;
                    FilterResult result = map_SkipFiguresUntilExponentOrNumberEnds[b];
                    if (result == FilterResult.Skip)
                    {
                        if (!TryNextByte())
                        {
                            couldNotSkip = true;
                            break;
                        }
                    }
                    else if (result == FilterResult.Found) break;
                    else if (result == FilterResult.Unexpected) return false;
                }
                count = bufferPos - startPos;
                if (couldNotSkip) count++;
                decimalBytes = new ArraySegment<byte>(buffer, startPos, count);
            }

            if (CurrentByte == 'e' || CurrentByte == 'E')
            {
                TryNextByte();
                // Read exponent part
                isExponentNegative = CurrentByte == '-';
                if (isExponentNegative) TryNextByte();
                startPos = bufferPos;
                while (true)
                {
                    byte b = CurrentByte;
                    FilterResult result = map_SkipFiguresUntilNumberEnds[b];
                    if (result == FilterResult.Skip)
                    {
                        if (!TryNextByte())
                        {
                            couldNotSkip = true;
                            break;
                        }
                    }
                    else if (result == FilterResult.Found) break;
                    else if (result == FilterResult.Unexpected) return false;
                }
                count = bufferPos - startPos;
                if (couldNotSkip) count++;
                exponentBytes = new ArraySegment<byte>(buffer, startPos, count);
            }

            return true;
        }

        bool TryReadStringBytes(out ArraySegment<byte> stringBytes)
        {
            stringBytes = default;

            // Skip whitespaces until string starts
            do
            {
                byte b = CurrentByte;
                var result = map_SkipWhitespacesUntilStringStarts[b];
                if (result == FilterResult.Found) break;
                else if (result == FilterResult.Unexpected) return false;
            } while (TryNextByte());
            
            int startPos = bufferPos+1;

            // Skip chars until string ends
            while (TryNextByte())
            {
                byte b = CurrentByte;
                FilterResult result = map_SkipCharsUntilStringEndsOrMultiByteChar[b];
                if (result == FilterResult.Skip) continue;
                else if (result == FilterResult.Found)
                {
                    if (b == (byte)'\\')
                    {
                        TryNextByte();
                    }
                    else if (b == (byte)'"')
                    {
                        stringBytes = new ArraySegment<byte>(buffer, startPos, bufferPos - startPos);
                        TryNextByte();
                        return true;
                    }
                    else if ((b & 0b11100000) == 0b11000000) // skip 1 byte
                    {
                        TryNextByte();
                    }
                    else if ((b & 0b11110000) == 0b11100000) // skip 2 bytes
                    {
                        TryNextByte();
                        TryNextByte();
                    }
                    else if ((b & 0b11111000) == 0b11110000) // skip 3 bytes
                    {
                        TryNextByte();
                        TryNextByte();
                        TryNextByte();
                    }
                }
                else return false;
            }
            return false;
        }

        void SkipWhiteSpaces()
        {
            do
            {
                var result = map_SkipWhitespaces[CurrentByte];
                if (result == FilterResult.Found) return;
            } while (TryNextByte());
        }
        
        bool CheckBytesRemaining(int numBytes)
        {
            return !(bufferPos + numBytes >= bufferFillLevel);
        }

        bool PeekNull()
        {
            if (!CheckBytesRemaining(3)) return false;            
            int peekPos = bufferPos;
            if (buffer[peekPos] != 'n' && buffer[peekPos] != 'N') return false;
            peekPos++;
            if (buffer[peekPos] != 'u' && buffer[peekPos] != 'U') return false;
            peekPos++;
            if (buffer[peekPos] != 'l' && buffer[peekPos] != 'L') return false;
            peekPos++;
            if (buffer[peekPos] != 'l' && buffer[peekPos] != 'L') return false;

            if (CheckBytesRemaining(4))
            {
                bufferPos += 4;
                if (map_IsFieldEnd[buffer[++peekPos]] != FilterResult.Found) return false;
            }
            else bufferPos += 3;

            return true;
        }

        enum FilterResult
        {
            Skip,
            Found,
            Unexpected
        }

        enum TypeResult
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

        static FilterResult[] CreateFilterMap_SkipWhitespacesUntilStringStarts()
        {
            FilterResult[] map = new FilterResult[256];
            for (int i = 0; i < map.Length; i++)
            {
                if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Skip;                
                else if (i == '\"') map[i] = FilterResult.Found;
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
