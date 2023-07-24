using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Threading;
using System.Collections;
using System.Globalization;
using FeatureLoom.Synchronization;
using System.Linq.Expressions;
using System.IO;
using System.Data;
using System.Runtime.CompilerServices;

namespace Playground
{
    public static partial class MyJsonSerializer
    {
        public class Settings
        {
            public TypeInfoHandling typeInfoHandling = TypeInfoHandling.AddDeviatingTypeInfo;
            public DataSelection dataSelection = DataSelection.PublicAndPrivateFields_CleanBackingFields;
            public ReferenceCheck referenceCheck = ReferenceCheck.AlwaysReplaceByRef;
            public int bufferSize = -1;
        }

        public enum DataSelection
        {
            PublicAndPrivateFields = 0,
            PublicAndPrivateFields_CleanBackingFields = 1,
            PublicFieldsAndProperties = 2,
        }

        public enum ReferenceCheck
        {
            NoRefCheck = 0,
            OnLoopThrowException = 1,
            OnLoopReplaceByNull = 2,
            OnLoopReplaceByRef = 3,
            AlwaysReplaceByRef = 4
        }

        public enum TypeInfoHandling
        {
            AddNoTypeInfo = 0,
            AddDeviatingTypeInfo = 1,
            AddAllTypeInfo = 2,
        }

        static Settings defaultSettings = new Settings();

        public static string Serialize<T>(T obj, Settings settings = null)
        {
            if (settings == null) settings = defaultSettings;

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;


            InternalSerializer<JsonStringWriter>.TypeCache typeCache = InternalSerializer<JsonStringWriter>.typeCaches[settings.dataSelection.ToInt()];
            Type objType = obj.GetType();

            bool autoBufferSize = false;
            if (settings.bufferSize < 0)
            {
                autoBufferSize = true;
                settings.bufferSize = 64;
                using (InternalSerializer<JsonStringWriter>.typeCacheLock.LockReadOnly())
                {
                    if (typeCache.typeBufferInfo.TryGetValue(objType, out var b)) settings.bufferSize = b;
                }
            }


            var writer = new JsonStringWriter(settings.bufferSize);
            InternalSerializer<JsonStringWriter>.Crawler crawler = InternalSerializer<JsonStringWriter>.Crawler.Root(obj, settings, writer);
            InternalSerializer<JsonStringWriter>.SerializeValue(obj, typeof(T), crawler);

            Thread.CurrentThread.CurrentCulture = oldCulture;

            if (autoBufferSize && writer.UsedBuffer > settings.bufferSize)
            {
                using (InternalSerializer<JsonStringWriter>.typeCacheLock.Lock())
                {
                    typeCache.typeBufferInfo[objType] = writer.UsedBuffer;
                }
            }
            return crawler.ToString();
        }

        public static byte[] SerializeToUtf8Bytes<T>(T obj, Settings settings = null)
        {
            if (settings == null) settings = defaultSettings;

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;


            InternalSerializer<JsonUTF8StreamWriter>.TypeCache typeCache = InternalSerializer<JsonUTF8StreamWriter>.typeCaches[settings.dataSelection.ToInt()];
            Type objType = obj.GetType();

            bool autoBufferSize = false;
            if (settings.bufferSize < 0)
            {
                autoBufferSize = true;
                settings.bufferSize = 64;
                using (InternalSerializer<JsonUTF8StreamWriter>.typeCacheLock.LockReadOnly())
                {
                    if (typeCache.typeBufferInfo.TryGetValue(objType, out var b)) settings.bufferSize = b;
                }
            }

            MemoryStream stream = new MemoryStream(settings.bufferSize);
            var writer = new JsonUTF8StreamWriter(stream);
            InternalSerializer<JsonUTF8StreamWriter>.Crawler crawler = InternalSerializer<JsonUTF8StreamWriter>.Crawler.Root(obj, settings, writer);
            InternalSerializer<JsonUTF8StreamWriter>.SerializeValue(obj, typeof(T), crawler);

            Thread.CurrentThread.CurrentCulture = oldCulture;

            if (autoBufferSize && stream.Length > settings.bufferSize)
            {
                using (InternalSerializer<JsonUTF8StreamWriter>.typeCacheLock.Lock())
                {
                    typeCache.typeBufferInfo[objType] = (int)stream.Length;
                }
            }
            return stream.ToArray();
        }

        public static void Serialize<T>(Stream stream, T obj, Settings settings = null)
        {
            if (settings == null) settings = defaultSettings;

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var writer = new JsonUTF8StreamWriter(stream);
            InternalSerializer<JsonUTF8StreamWriter>.Crawler crawler = InternalSerializer<JsonUTF8StreamWriter>.Crawler.Root(obj, settings, writer);
            InternalSerializer<JsonUTF8StreamWriter>.SerializeValue(obj, typeof(T), crawler);

            Thread.CurrentThread.CurrentCulture = oldCulture;
        }

        private static partial class InternalSerializer<T> where T : MyJsonSerializer.IJsonWriter
        {


            public class TypeCache
            {
                public Dictionary<Type, List<MemberInfo>> memberInfos = new Dictionary<Type, List<MemberInfo>>();
                public Dictionary<MemberInfo, Action<object, MemberInfo, Crawler>> fieldWriters = new Dictionary<MemberInfo, Action<object, MemberInfo, Crawler>>();
                public Dictionary<Type, int> typeBufferInfo = new Dictionary<Type, int>();
            }
            public static TypeCache[] typeCaches = InitTypeCaches();
            public static FeatureLock typeCacheLock = new FeatureLock();

            private static TypeCache[] InitTypeCaches()
            {
                int numTypeCaches = Enum.GetValues(typeof(DataSelection)).Length;
                TypeCache[] typeCaches = new TypeCache[numTypeCaches];
                for (int i = 0; i < typeCaches.Length; i++)
                {
                    typeCaches[i] = new TypeCache();
                }
                return typeCaches;
            }


            public static void SerializeValue(object obj, Type expectedType, Crawler crawler)
            {
                if (obj == null)
                {
                    crawler.writer.WriteNullValue();
                    return;
                }

                Type objType = obj.GetType();
                bool deviatingType = expectedType != obj.GetType();

                if (objType.IsPrimitive)
                {
                    PrepareUnexpectedValue(crawler, deviatingType, objType);
                    crawler.writer.WritePrimitiveValue(obj);
                    FinishUnexpectedValue(crawler, deviatingType);
                    return;
                }
                if (obj is string str)
                {
                    PrepareUnexpectedValue(crawler, deviatingType, objType);
                    crawler.writer.WriteStringValue(str);
                    FinishUnexpectedValue(crawler, deviatingType);
                    return;
                }

                if (CheckWritingRefObject(crawler)) return;

                if (obj is IEnumerable items && (obj is ICollection || objType.ImplementsGenericInterface(typeof(ICollection<>))))
                {
                    PrepareUnexpectedValue(crawler, deviatingType, objType);
                    SerializeCollection(objType, items, crawler);
                    FinishUnexpectedValue(crawler, deviatingType);
                    return;
                }

                SerializeComplexType(obj, expectedType, crawler);
                return;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool CheckWritingRefObject(Crawler crawler)
            {
                if (crawler.settings.referenceCheck == ReferenceCheck.NoRefCheck || crawler.refPath == null) return false;

                if (crawler.settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef)
                {
                    crawler.writer.WriteRefObject(crawler.refPath);
                }
                else if (crawler.currentPath.StartsWith(crawler.refPath))
                {
                    if (crawler.settings.referenceCheck == ReferenceCheck.OnLoopReplaceByRef)
                    {
                        crawler.writer.WriteRefObject(crawler.refPath);
                    }
                    else if (crawler.settings.referenceCheck == ReferenceCheck.OnLoopReplaceByNull)
                    {
                        crawler.writer.WriteNullValue();
                    }
                    else if (crawler.settings.referenceCheck == ReferenceCheck.OnLoopThrowException)
                    {
                        throw new Exception("Circular referencing detected!");
                    }
                }

                return true;
            }



            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void PrepareUnexpectedValue(Crawler crawler, bool deviatingType, Type objType)
            {
                if (crawler.settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (crawler.settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
                {
                    crawler.writer.OpenObject();
                    crawler.writer.WriteTypeInfo(objType.FullName);
                    crawler.writer.WriteValueFieldName();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void FinishUnexpectedValue(Crawler crawler, bool deviatingType)
            {
                if (crawler.settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (crawler.settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
                {
                    crawler.writer.CloseObject();
                }
            }




            private static void SerializeCollection(Type objType, IEnumerable items, Crawler crawler)
            {

                if (items is IEnumerable<string> string_items) SerializeStringCollection(string_items, crawler);
                else if (items is IEnumerable<int> int_items) SerializePrimitiveCollection(int_items, crawler);
                else if (items is IEnumerable<uint> uint_items) SerializePrimitiveCollection(uint_items, crawler);
                else if (items is IEnumerable<byte> byte_items) SerializePrimitiveCollection(byte_items, crawler);
                else if (items is IEnumerable<sbyte> sbyte_items) SerializePrimitiveCollection(sbyte_items, crawler);
                else if (items is IEnumerable<short> short_items) SerializePrimitiveCollection(short_items, crawler);
                else if (items is IEnumerable<ushort> ushort_items) SerializePrimitiveCollection(ushort_items, crawler);
                else if (items is IEnumerable<long> long_items) SerializePrimitiveCollection(long_items, crawler);
                else if (items is IEnumerable<ulong> ulong_items) SerializePrimitiveCollection(ulong_items, crawler);
                else if (items is IEnumerable<bool> bool_items) SerializePrimitiveCollection(bool_items, crawler);
                else if (items is IEnumerable<char> char_items) SerializePrimitiveCollection(char_items, crawler);
                else if (items is IEnumerable<float> float_items) SerializePrimitiveCollection(float_items, crawler);
                else if (items is IEnumerable<double> double_items) SerializePrimitiveCollection(double_items, crawler);
                else if (items is IEnumerable<IntPtr> intPtr_items) SerializePrimitiveCollection(intPtr_items, crawler);
                else if (items is IEnumerable<UIntPtr> uIntPtr_items) SerializePrimitiveCollection(uIntPtr_items, crawler);
                else
                {
                    Type collectionType = objType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>));
                    collectionType = collectionType ?? typeof(object);

                    crawler.writer.OpenCollection();
                    bool isFirstItem = true;
                    int index = 0;
                    foreach (var item in items)
                    {
                        if (isFirstItem) isFirstItem = false;
                        else crawler.writer.WriteComma();
                        if (item != null && !item.GetType().IsPrimitive && !(item is string))
                        {
                            SerializeValue(item, collectionType, crawler.NewCollectionItem(item, "", index++));
                        }
                        else
                        {
                            SerializeValue(item, collectionType, crawler);
                        }
                    }
                    crawler.writer.CloseCollection();
                }
            }

            private static void SerializeComplexType(object obj, Type expectedType, Crawler crawler)
            {
                var settings = crawler.settings;

                Type objType = obj.GetType();

                if (objType.IsNullable() && obj == null)
                {
                    crawler.writer.WriteNullValue();
                    return;
                }

                bool isFirstField = true;
                crawler.writer.OpenObject();

                bool deviatingType = expectedType != obj.GetType();
                if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
                {
                    if (isFirstField) isFirstField = false;
                    crawler.writer.WriteTypeInfo(objType.FullName);
                }

                TypeCache typeCache = typeCaches[settings.dataSelection.ToInt()];
                List<MemberInfo> members;
                using (var lockHandle = typeCacheLock.LockReadOnly())
                {
                    if (!typeCache.memberInfos.TryGetValue(objType, out members))
                    {
                        lockHandle.UpgradeToWriteMode();

                        members = new List<MemberInfo>();
                        if (settings.dataSelection == DataSelection.PublicFieldsAndProperties)
                        {
                            members.AddRange(objType.GetFields(BindingFlags.Public | BindingFlags.Instance));
                            members.AddRange(objType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.GetMethod != null));
                        }
                        else
                        {
                            members.AddRange(objType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                            Type t = objType.BaseType;
                            while (t != null)
                            {
                                members.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !members.Any(field => field.Name == baseField.Name)));
                                t = t.BaseType;
                            }
                        }
                        typeCache.memberInfos[objType] = members;
                    }
                }

                foreach (var field in members)
                {
                    if (isFirstField) isFirstField = false;
                    else crawler.writer.WriteComma();

                    Action<object, MemberInfo, Crawler> writer;
                    using (var lockHandle = typeCacheLock.LockReadOnly())
                    {
                        if (!typeCache.fieldWriters.TryGetValue(field, out writer))
                        {
                            lockHandle.UpgradeToWriteMode();
                            writer = CreateFieldWriter(objType, field, settings);
                            typeCache.fieldWriters[field] = writer;
                        }
                    }
                    writer.Invoke(obj, field, crawler);
                }
                crawler.writer.CloseObject();
            }

            private static Action<object, MemberInfo, Crawler> CreateFieldWriter(Type objType, MemberInfo member, Settings settings)
            {
                Type memberType = member is FieldInfo field ? field.FieldType : member is PropertyInfo property ? property.PropertyType : default;

                if (memberType == typeof(string)) return CreateStringFieldWriter(objType, member, settings);
                else if (memberType == typeof(int)) return CreatePrimitiveFieldWriter<int>(objType, member, settings);
                else if (memberType == typeof(uint)) return CreatePrimitiveFieldWriter<uint>(objType, member, settings);
                else if (memberType == typeof(byte)) return CreatePrimitiveFieldWriter<byte>(objType, member, settings);
                else if (memberType == typeof(sbyte)) return CreatePrimitiveFieldWriter<sbyte>(objType, member, settings);
                else if (memberType == typeof(short)) return CreatePrimitiveFieldWriter<short>(objType, member, settings);
                else if (memberType == typeof(ushort)) return CreatePrimitiveFieldWriter<ushort>(objType, member, settings);
                else if (memberType == typeof(long)) return CreatePrimitiveFieldWriter<long>(objType, member, settings);
                else if (memberType == typeof(ulong)) return CreatePrimitiveFieldWriter<ulong>(objType, member, settings);
                else if (memberType == typeof(bool)) return CreatePrimitiveFieldWriter<bool>(objType, member, settings);
                else if (memberType == typeof(char)) return CreatePrimitiveFieldWriter<char>(objType, member, settings);
                else if (memberType == typeof(float)) return CreatePrimitiveFieldWriter<float>(objType, member, settings);
                else if (memberType == typeof(double)) return CreatePrimitiveFieldWriter<double>(objType, member, settings);
                else if (memberType == typeof(IntPtr)) return CreatePrimitiveFieldWriter<IntPtr>(objType, member, settings);
                else if (memberType == typeof(UIntPtr)) return CreatePrimitiveFieldWriter<UIntPtr>(objType, member, settings);
                else if (memberType.IsAssignableTo(typeof(IEnumerable)) &&
                            (memberType.IsAssignableTo(typeof(ICollection)) ||
                             memberType.IsOfGenericType(typeof(ICollection<>))))
                {

                    return CreateCollectionFieldWriter(member, settings);
                }

                string fieldName = PrepareFieldName(member, settings);

                Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
                {
                    crawler.writer.WritePreparedString(fieldName);
                    var (memberType, value) = member is FieldInfo field ? (field.FieldType, field.GetValue(obj)) :
                                              member is PropertyInfo property ? (property.PropertyType, property.GetValue(obj)) :
                                              default;
                    SerializeValue(value, memberType, crawler.NewChild(value, member.Name));
                };

                return writer;
            }

            private static void SerializePrimitiveCollection<T>(IEnumerable<T> items, Crawler crawler)
            {
                crawler.writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else crawler.writer.WriteComma();
                    crawler.writer.WritePrimitiveValue(item);
                }
                crawler.writer.CloseCollection();
            }

            private static void SerializeStringCollection(IEnumerable<string> items, Crawler crawler)
            {
                crawler.writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else crawler.writer.WriteComma();
                    crawler.writer.WriteStringValue(item);
                }
                crawler.writer.CloseCollection();
            }

            private static Action<object, MemberInfo, Crawler> CreatePrimitiveFieldWriter<T>(Type objType, MemberInfo member, Settings settings)
            {
                string fieldName = PrepareFieldName(member, settings);

                var parameter = Expression.Parameter(typeof(object));
                var castedParameter = Expression.Convert(parameter, objType);
                var fieldAccess = member is FieldInfo field ? Expression.Field(castedParameter, field) : member is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
                var lambda = Expression.Lambda<Func<object, T>>(fieldAccess, parameter);
                var compiledGetter = lambda.Compile();

                Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
                {
                    var value = compiledGetter(obj);
                    //var value = (T) (member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                    crawler.writer.WritePreparedString(fieldName);
                    if (value != null) PrepareUnexpectedValue(crawler, false, value.GetType());
                    crawler.writer.WritePrimitiveValue(value);
                    if (value != null) FinishUnexpectedValue(crawler, false);
                };

                return writer;
            }

            private static Action<object, MemberInfo, Crawler> CreateStringFieldWriter(Type objType, MemberInfo member, Settings settings)
            {
                string fieldName = PrepareFieldName(member, settings);

                var parameter = Expression.Parameter(typeof(object));
                var castedParameter = Expression.Convert(parameter, objType);
                var fieldAccess = member is FieldInfo field ? Expression.Field(castedParameter, field) : member is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
                var lambda = Expression.Lambda<Func<object, string>>(fieldAccess, parameter);
                var compiledGetter = lambda.Compile();

                Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
                {
                    var value = compiledGetter(obj);
                    //var value = (string) (member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                    crawler.writer.WritePreparedString(fieldName);
                    if (value != null) PrepareUnexpectedValue(crawler, false, value.GetType());
                    crawler.writer.WriteStringValue(value);
                    FinishUnexpectedValue(crawler, false);
                };

                return writer;
            }

            private static Action<object, MemberInfo, Crawler> CreatePrimitiveCollectionFieldWriter<T>(MemberInfo member, Settings settings)
            {
                string fieldName = PrepareFieldName(member, settings);

                var parameter = Expression.Parameter(typeof(object));
                Type declaringType = member is FieldInfo field2 ? field2.DeclaringType : member is PropertyInfo property2 ? property2.DeclaringType : default;
                var castedParameter = Expression.Convert(parameter, declaringType);
                var fieldAccess = member is FieldInfo field3 ? Expression.Field(castedParameter, field3) : member is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
                var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<T>));
                var lambda = Expression.Lambda<Func<object, IEnumerable<T>>>(castFieldAccess, parameter);
                var compiledGetter = lambda.Compile();

                Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
                {
                    //IEnumerable<T> items = (IEnumerable<T>) (member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                    IEnumerable<T> items = compiledGetter(obj);
                    crawler.writer.WritePreparedString(fieldName);
                    PrepareUnexpectedValue(crawler, false, items.GetType());
                    crawler.writer.OpenCollection();
                    bool isFirstItem = true;
                    foreach (var item in items)
                    {
                        if (isFirstItem) isFirstItem = false;
                        else crawler.writer.WriteComma();
                        PrepareUnexpectedValue(crawler, false, item.GetType());
                        crawler.writer.WritePrimitiveValue(item);
                        FinishUnexpectedValue(crawler, false);
                    }
                    crawler.writer.CloseCollection();
                    FinishUnexpectedValue(crawler, false);
                };

                return writer;
            }

            private static Action<object, MemberInfo, Crawler> CreateStringCollectionFieldWriter(MemberInfo member, Settings settings)
            {
                string fieldName = PrepareFieldName(member, settings);

                var parameter = Expression.Parameter(typeof(object));
                Type declaringType = member is FieldInfo field2 ? field2.DeclaringType : member is PropertyInfo property2 ? property2.DeclaringType : default;
                var castedParameter = Expression.Convert(parameter, declaringType);
                var fieldAccess = member is FieldInfo field3 ? Expression.Field(castedParameter, field3) : member is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
                var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<string>));
                var lambda = Expression.Lambda<Func<object, IEnumerable<string>>>(castFieldAccess, parameter);
                var compiledGetter = lambda.Compile();

                Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
                {
                    //IEnumerable<string> items = (IEnumerable<string>)(member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                    IEnumerable<string> items = compiledGetter(obj);
                    crawler.writer.WritePreparedString(fieldName);
                    PrepareUnexpectedValue(crawler, false, items.GetType());
                    crawler.writer.OpenCollection();
                    bool isFirstItem = true;
                    foreach (var item in items)
                    {
                        if (isFirstItem) isFirstItem = false;
                        else crawler.writer.WriteComma();
                        PrepareUnexpectedValue(crawler, false, item.GetType());
                        crawler.writer.WriteStringValue(item);
                        FinishUnexpectedValue(crawler, false);
                    }
                    crawler.writer.CloseCollection();
                    FinishUnexpectedValue(crawler, false);
                };

                return writer;
            }

            private static Action<object, MemberInfo, Crawler> CreateCollectionFieldWriter(MemberInfo member, Settings settings)
            {
                Type collectionType = member is FieldInfo field ? field.FieldType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                      member is PropertyInfo property ? property.PropertyType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                      default;
                collectionType = collectionType ?? typeof(object);

                if (collectionType == typeof(string)) return CreateStringCollectionFieldWriter(member, settings);
                else if (collectionType == typeof(int)) return CreatePrimitiveCollectionFieldWriter<int>(member, settings);
                else if (collectionType == typeof(uint)) return CreatePrimitiveCollectionFieldWriter<uint>(member, settings);
                else if (collectionType == typeof(byte)) return CreatePrimitiveCollectionFieldWriter<byte>(member, settings);
                else if (collectionType == typeof(sbyte)) return CreatePrimitiveCollectionFieldWriter<sbyte>(member, settings);
                else if (collectionType == typeof(short)) return CreatePrimitiveCollectionFieldWriter<short>(member, settings);
                else if (collectionType == typeof(ushort)) return CreatePrimitiveCollectionFieldWriter<ushort>(member, settings);
                else if (collectionType == typeof(long)) return CreatePrimitiveCollectionFieldWriter<long>(member, settings);
                else if (collectionType == typeof(ulong)) return CreatePrimitiveCollectionFieldWriter<ulong>(member, settings);
                else if (collectionType == typeof(bool)) return CreatePrimitiveCollectionFieldWriter<bool>(member, settings);
                else if (collectionType == typeof(char)) return CreatePrimitiveCollectionFieldWriter<char>(member, settings);
                else if (collectionType == typeof(float)) return CreatePrimitiveCollectionFieldWriter<float>(member, settings);
                else if (collectionType == typeof(double)) return CreatePrimitiveCollectionFieldWriter<double>(member, settings);
                else if (collectionType == typeof(IntPtr)) return CreatePrimitiveCollectionFieldWriter<IntPtr>(member, settings);
                else if (collectionType == typeof(UIntPtr)) return CreatePrimitiveCollectionFieldWriter<UIntPtr>(member, settings);

                string fieldName = PrepareFieldName(member, settings);

                var parameter = Expression.Parameter(typeof(object));
                Type declaringType = member is FieldInfo field2 ? field2.DeclaringType : member is PropertyInfo property2 ? property2.DeclaringType : default;
                var castedParameter = Expression.Convert(parameter, declaringType);
                var fieldAccess = member is FieldInfo field3 ? Expression.Field(castedParameter, field3) : member is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
                var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable));
                var lambda = Expression.Lambda<Func<object, IEnumerable>>(castFieldAccess, parameter);
                var compiledGetter = lambda.Compile();

                Action<object, MemberInfo, Crawler> writer = (obj, member, crawler) =>
                {
                    //IEnumerable items = (IEnumerable)(member is FieldInfo field ? field.GetValue(obj) : member is PropertyInfo property ? property.GetValue(obj) : default);
                    IEnumerable items = compiledGetter(obj);
                    crawler.writer.WritePreparedString(fieldName);
                    PrepareUnexpectedValue(crawler, false, items.GetType());
                    crawler.writer.OpenCollection();
                    bool isFirstItem = true;
                    int index = 0;
                    foreach (var item in items)
                    {
                        if (isFirstItem) isFirstItem = false;
                        else crawler.writer.WriteComma();
                        if (item != null && !item.GetType().IsPrimitive && !(item is string))
                        {
                            SerializeValue(item, collectionType, crawler.NewCollectionItem(item, member.Name, index));
                        }
                        else
                        {
                            SerializeValue(item, collectionType, crawler);
                        }
                        index++;
                    }
                    crawler.writer.CloseCollection();
                    FinishUnexpectedValue(crawler, false);
                };

                return writer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static string PrepareFieldName(MemberInfo member, Settings settings)
            {
                if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                    member.Name.StartsWith('<') &&
                    member.Name.EndsWith(">k__BackingField")) return $"\"{member.Name.Substring("<", ">")}\":";

                return $"\"{member.Name}\":";
            }


        }
    }
}
