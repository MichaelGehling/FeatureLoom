using FeatureLoom.Synchronization;
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
using Microsoft.Extensions.Hosting;

namespace Playground
{
    public sealed partial class FeatureJsonDeserializer
    {
        MicroValueLock serializerLock = new MicroValueLock();
        const int BUFFER_SIZE = 1024 * 64;
        byte[] buffer = new byte[BUFFER_SIZE];
        Stack<int> peekStack = new Stack<int>();
        int bufferPos = 0;
        int pinnedBufferPos = -1;
        int bufferFillLevel = 0;
        int bufferResetLevel = BUFFER_SIZE - (1024 * 8);
        long totalBytesRead = 0;
        Stream stream;                
        private SlicedBuffer<byte> tempSlicedBuffer;
        EquatableByteSegment rootName = "$".ToByteArray();
        EquatableByteSegment currentItemName = "$".ToByteArray();
        int currentItemInfoIndex = -1;
        List<ItemInfo> itemInfos = new List<ItemInfo>();

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

        static ulong[] exponentFactorMap = CreateExponentFactorMap();

        struct ItemInfo
        {
            public EquatableByteSegment name;
            public int parentIndex;
            public object itemRef;

            public ItemInfo(EquatableByteSegment name, int parentIndex)
            {
                this.name = name;
                this.parentIndex = parentIndex;
            }
        }

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
            tempSlicedBuffer = new SlicedBuffer<byte>(128 * 1024, 256, 128 * 1024);
        }

        private void Reset()
        {       
            currentItemName = rootName;
            tempSlicedBuffer.Reset(true, false);
            itemInfos.Clear();
            currentItemInfoIndex = -1;
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
                    // TODO
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

        private void SetItemRefInCurrentItemInfo(object item)
        {
            var itemInfo = itemInfos[currentItemInfoIndex];
            itemInfo.itemRef = item;
            itemInfos[currentItemInfoIndex] = itemInfo;
        }

        private Func<T> GetConstructor<T>()
        {
            Func<T> constructor = null;
            Type type = typeof(T);
            if (settings.constructors.TryGetValue(type, out object c) && c is Func<T> typedConstructor) constructor = typedConstructor;
            else if (null != type.GetConstructor(Array.Empty<Type>())) constructor = CompileConstructor<T>();
            else if (type.IsValueType) return () => default;

            if (constructor == null) throw new Exception($"No default constructor for type {TypeNameHelper.GetSimplifiedTypeName(type)}. Use AddConstructor in Settings.");

            if (!type.IsValueType) // TODO check for ref-support active
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
            Type type = typeof(T);
            Func<P, T> constructor = CompileConstructor<T, P>();

            if (!type.IsValueType) // TODO check for ref-support active
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
            var constructor = GetConstructor<T>();
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

                        K fieldName = keyReader.ReadFieldName<K>(out var fieldNameBytes);
                        SkipWhiteSpaces();
                        if (CurrentByte != ':') throw new Exception("Failed reading object to Dictionary");
                        TryNextByte();
                        V value = valueReader.ReadValue<V>(fieldNameBytes);
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
                string name = memberInfo.Name;
                var itemFieldName = new EquatableByteSegment(name.ToByteArray());
                var itemFieldWriter = this.InvokeGenericMethod<Func<T,T>>(nameof(CreateItemFieldWriter), new Type[] { itemType, fieldType }, memberInfo, itemFieldName);                
                if (name.TryExtract("<{name}>k__BackingField", out string backingFieldName)) name = backingFieldName;                
                itemFieldWriters[itemFieldName] = itemFieldWriter;
            }
/*
            // Add ref handler
            itemFieldWriters[new EquatableByteSegment("$Ref".ToByteArray())] = item =>
            {
                if (!TryReadStringBytes(out var stringBytes)) throw new Exception("Failed reading string");
                if (!refItems.TryGetValue(stringBytes, out object refObj)) return item;
                if (!(refObj is T refItem)) return item;
                
                SkipWhiteSpaces();
                if (CurrentByte == '}') return refItem;

                // Skip all following fields until the object's end is found
                if (CurrentByte == ',') TryNextByte();
                while (true)
                {
                    SkipWhiteSpaces();
                    if (CurrentByte == '}') break;

                    if (!TryReadStringBytes(out var _)) throw new Exception("Failed reading object");
                    SkipWhiteSpaces();
                    if (CurrentByte != ':') throw new Exception("Failed reading object");
                    TryNextByte();
                    SkipValue();
                    SkipWhiteSpaces();
                    if (CurrentByte == ',') TryNextByte();
                }

                return refItem;
            };
*/
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


        private Func<T,T> CreateItemFieldWriter<T, V>(MemberInfo memberInfo, EquatableByteSegment fieldName)
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
                            V value = fieldTypeReader.ReadValue<V>(fieldName);
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
                            fieldInfo.SetValue(parentItem, value);
                            return parentItem;
                        };
                    }
                }
                else
                {
                    // Use expression tree for normal writable field
                    return CreateFieldWriterUsingExpression<T, V>(fieldInfo, fieldName);
                }
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                if (propertyInfo.CanWrite)
                {
                    // Use expression tree for writable property
                    return CreatePropertyWriterUsingExpression<T, V>(propertyInfo, fieldName);
                }
                else if (HasInitAccessor(propertyInfo))
                {
                    // Handle init-only properties
                    var fieldTypeReader = GetCachedTypeReader(fieldType);
                    if (itemType.IsValueType)
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

            throw new InvalidOperationException("MemberInfo must be a writable field, property, or init-only property.");
        }

        private bool HasInitAccessor(PropertyInfo propertyInfo)
        {
            // Use reflection to check if the property has an init accessor
            var setMethod = propertyInfo.SetMethod;
            return setMethod != null && setMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
        }

        private Func<T,T> CreateFieldWriterUsingExpression<T, V>(FieldInfo fieldInfo, EquatableByteSegment fieldName)
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
                V value = fieldTypeReader.ReadValue<V>(fieldName);
                setValue(parentItem, value);
                return parentItem;
            };
        }

        private Func<T,T> CreatePropertyWriterUsingExpression<T, V>(PropertyInfo propertyInfo, EquatableByteSegment fieldName)
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
                V value = fieldTypeReader.ReadValue<V>(fieldName);
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
                SetItemRefInCurrentItemInfo(item);
                pool.Return(elementBuffer);
                if (CurrentByte != ']') throw new Exception("Failed reading Array");
                TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);

        }

        private bool TryCreateEnumerableTypeReader(Type itemType, CachedTypeReader cachedTypeReader)
        {
            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType))
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

        private void CreateGenericEnumerableTypeReader<T, E>(CachedTypeReader cachedTypeReader)
        {
            var elementReader = new ElementReader<E>(this);

            var constructor = GetConstructor<T, IEnumerable<E>>();

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

        private void CreateEnumerableTypeReader<T>(CachedTypeReader cachedTypeReader)
        {
            var elementReader = new ElementReader<object>(this);

            var constructor = GetConstructor<T, IEnumerable>();

            Pool<List<object>> pool = new Pool<List<object>>(() => new List<object>(), l => l.Clear());
            cachedTypeReader.SetTypeReader<T>(() =>
            {
                SkipWhiteSpaces();
                if (CurrentByte != '[') throw new Exception("Failed reading Array");
                if (!TryNextByte()) throw new Exception("Failed reading Array");
                List<object> elementBuffer = pool.Take();
                elementBuffer.AddRange(elementReader);
                T item = constructor(elementBuffer);
                pool.Return(elementBuffer);
                if (CurrentByte != ']') throw new Exception("Failed reading Array");
                TryNextByte();
                return item;
            }, JsonDataTypeCategory.Array);
        }

        List<EquatableByteSegment> arrayElementNameCache = new List<EquatableByteSegment>();
        EquatableByteSegment GetArrayElementName(int index)
        {
            while (arrayElementNameCache.Count <= index)
            {
                arrayElementNameCache.Add(new EquatableByteSegment($"[{index}]".ToByteArray()));
            }               
            return arrayElementNameCache[index];            
        }

        class ElementReader<T> : IEnumerable<T>, IEnumerator<T>, IEnumerable, IEnumerator
        {
            FeatureJsonDeserializer deserializer;
            CachedTypeReader reader;
            T current = default;
            int index = -1;

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
                if (deserializer.CurrentByte == ']')
                {
                    Reset();
                    return false;
                }
                current = reader.ReadValue<T>(deserializer.GetArrayElementName(++index));
                deserializer.SkipWhiteSpaces();
                if (deserializer.CurrentByte == ',') deserializer.TryNextByte();
                else if (deserializer.CurrentByte != ']')
                {
                    Reset();
                    throw new Exception("Failed reading Array");
                }
                return true;               
            }

            public void Reset()
            {
                index = -1;
                current = default;
            }

            public void Dispose()
            {
                Reset();
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
                item = reader.ReadValue<T>(rootName);
                return true;
            }
            catch
            {
                item = default;
                return false;
            }
            finally
            {
                Reset();
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

        private bool TrySkipBytes(int numBytesToSkip)
        {
            if (!CheckBytesRemaining(numBytesToSkip)) return false;
            bufferPos += numBytesToSkip;
            return true;
        }

        private byte CurrentByte => buffer[bufferPos];

        void SkipValue()
        {
            SkipWhiteSpaces();

            var valueType = map_TypeStart[CurrentByte];
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
            if (!TryReadNull()) throw new Exception("Failed reading null value");
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

            if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) throw new Exception("Failed reading object");
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
            peekStack.Push(bufferPos);
            bool success = Try(out isNegative, out integerBytes, out decimalBytes, out exponentBytes, out isExponentNegative);
            if (success) peekStack.Pop();
            else bufferPos = peekStack.Pop();
            return success;

            bool Try(out bool isNegative, out ArraySegment<byte> integerBytes, out ArraySegment<byte> decimalBytes, out ArraySegment<byte> exponentBytes, out bool isExponentNegative)
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
        }

        bool TryReadStringBytes(out ArraySegment<byte> stringBytes)
        {
            peekStack.Push(bufferPos);
            bool success = Try(out stringBytes);
            if (success) peekStack.Pop();
            else bufferPos = peekStack.Pop();
            return success;

            bool Try(out ArraySegment<byte> stringBytes)
            {
                stringBytes = default;

                // Skip whitespaces until string starts
                SkipWhiteSpaces();
                if (CurrentByte != (byte)'"') return false;

                int startPos = bufferPos + 1;

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
        }

        void SkipWhiteSpaces()
        {
            do
            {
                if (map_SkipWhitespaces[CurrentByte] == FilterResult.Found) return;
            } while (TryNextByte());
        }
        
        bool CheckBytesRemaining(int numBytes)
        {
            return !(bufferPos + numBytes >= bufferFillLevel);
        }

        int CountRemainingBytes() => bufferFillLevel - bufferPos;

        EquatableByteSegment refFieldName = "$ref".ToByteArray();
        List<EquatableByteSegment> fieldPathSegments = new List<EquatableByteSegment>();

        bool TryReadRefObject<T>(out bool pathIsValid, out bool typeIsCompatible, out T refObject)
        {
            peekStack.Push(bufferPos);
            bool success = Try(out pathIsValid, out typeIsCompatible, out refObject);
            if (success) peekStack.Pop();
            else bufferPos = peekStack.Pop();

            fieldPathSegments.Clear();
            return success;

            bool Try(out bool pathIsValid, out bool typeIsCompatible, out T itemRef)
            {
                pathIsValid = false;
                typeIsCompatible = false;
                itemRef = default;

                // IMPORTANT: At the moment, the ref-field must be the first, TODO: add option to allow it to be anywhere in the object (which is much slower)
                SkipWhiteSpaces();
                if (CurrentByte != (byte)'{') return false;
                TryNextByte();
                if (!TryReadStringBytes(out var fieldName)) return false;
                if (!refFieldName.Equals(fieldName)) return false;
                SkipWhiteSpaces();
                if (CurrentByte != (byte)':') return false;
                TryNextByte();
                if (!TryReadStringBytes(out var refPath)) return false;
                SkipWhiteSpaces();
                if (CurrentByte == ',') TryNextByte();

                // Skip the rest
                while (true)
                {
                    SkipWhiteSpaces();
                    if (CurrentByte == '}') break;

                    if (!TryReadStringBytes(out var _)) return false;
                    SkipWhiteSpaces();
                    if (CurrentByte != ':') return false;
                    TryNextByte();
                    SkipValue();
                    SkipWhiteSpaces();
                    if (CurrentByte == ',') TryNextByte();
                }
                if (TryNextByte() && map_IsFieldEnd[CurrentByte] != FilterResult.Found) return false;

                
                
                // TODO find object
                if (refPath.Count <= 0) return false;
                int pos = 0;
                int startPos = 0;
                int segmentLength = 0;
                int refPathCount = refPath.Count;
                byte b = refPath[pos];

                while (true)
                {
                    if (b == '[')
                    {
                        while (true)
                        {
                            pos++;
                            if (pos >= refPathCount) return false;
                            b = refPath[pos];
                            if (b == ']')
                            {
                                segmentLength = pos - startPos + 1;
                                pos++;
                                break;
                            }
                        }
                        EquatableByteSegment segment = refPath.Slice(startPos, segmentLength);
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
                            b = refPath[pos];
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
                        EquatableByteSegment segment = refPath.Slice(startPos, segmentLength);
                        fieldPathSegments.Add(segment);
                        if (pos >= refPathCount) break;
                    }
                    startPos = pos;
                    b = refPath[pos];
                }


                object potentialItemRef = null;
                int lastSegmentIndex = fieldPathSegments.Count - 1;
                var referencedFieldName = fieldPathSegments[lastSegmentIndex];
                foreach(var info in itemInfos)
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

        bool TryReadNull()
        {

            peekStack.Push(bufferPos);
            bool success = Try();
            if (success) peekStack.Pop();
            else bufferPos = peekStack.Pop();
            return success;

            bool Try()
            {
                SkipWhiteSpaces();
                byte b;

                if (!TryNextByte()) return false;
                b = CurrentByte;
                if (b != 'n' && b != 'N') return false;
                if (!TryNextByte()) return false;
                b = CurrentByte;
                if (b != 'u' && b != 'U') return false;
                if (!TryNextByte()) return false;
                b = CurrentByte;
                if (b != 'l' && b != 'L') return false;
                if (!TryNextByte()) return false;
                b = CurrentByte;
                if (b != 'l' && b != 'L') return false;

                // Check for field end
                if (!TryNextByte()) return true;
                b = CurrentByte;
                if (map_IsFieldEnd[b] != FilterResult.Found) return false;

                return true;
            }
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
