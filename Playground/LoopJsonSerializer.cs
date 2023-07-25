using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Extensions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using System.Data;
using Newtonsoft.Json.Linq;
using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using System.Collections.Specialized;

namespace Playground
{
    public sealed partial class LoopJsonSerializer
    {
        
        interface IJob
        {

        }

        class CollectionJob : IJob
        {
            public object item;
            public Type itemType;
            public Type collectionType;
            public int index;
            public IEnumerator enumerator;
            public string currentPath;
            public bool deviatingType;
        }

        class ComplexJob : IJob
        {
            public object item;
            public Type itemType;
            public bool firstChild;
            public IEnumerator enumerator;
            public string currentPath;
            public bool deviatingType;
        }        

        Stack<IJob> jobStack = new Stack<IJob>();        
        static Settings defaultSettings = new();
        Settings settings;
        JsonUTF8StreamWriter writer = new JsonUTF8StreamWriter();
        MemoryStream memoryStream = new MemoryStream();
        Dictionary<object, string> pathMap = new();
        Dictionary<Type, TypeCacheItem> typeCache = new();

        class TypeCacheItem
        {
            public bool isCollection;
            public Type collectionType;
            public List<Action<object, string>> fieldWriters;
        }

        class CollectionInfo
        {
            public bool isCollection;
            public Type collectionType;

            public readonly static CollectionInfo NoCollectionInfo = new CollectionInfo(false, null);
            public readonly static CollectionInfo ObjectCollectionInfo = new CollectionInfo(false, typeof(object));

            public CollectionInfo(bool isCollection, Type collectionType)
            {
                this.isCollection = isCollection;
                this.collectionType = collectionType;
            }
        }        


        public string Serialize<T>(T item, Settings settings = null)
        {
            memoryStream.Position = 0;
            pathMap.Clear();
            if (settings == null) settings = defaultSettings;
            this.settings = settings;
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            writer.stream = memoryStream;
            HandleItem(item, typeof(T), "$");
            Loop();
            Thread.CurrentThread.CurrentCulture = oldCulture;
            return Encoding.UTF8.GetString(memoryStream.ToArray());
        }

        public byte[] SerializeToUtf8Bytes<T>(T item, Settings settings = null)
        {
            memoryStream.Position = 0;
            pathMap.Clear();
            if (settings == null) settings = defaultSettings;
            this.settings = settings;
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            writer.stream = memoryStream;
            HandleItem(item, typeof(T), "$");
            Loop();
            Thread.CurrentThread.CurrentCulture = oldCulture;
            return memoryStream.ToArray();
        }

        public void Serialize<T>(Stream stream, T item, Settings settings = null)
        {
            pathMap.Clear();
            if (settings == null) settings = defaultSettings;
            this.settings = settings;
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            writer.stream = stream;
            HandleItem(item, typeof(T), "$");
            Loop();
            Thread.CurrentThread.CurrentCulture = oldCulture;
        }

        private void Loop()
        {
            while (jobStack.TryPop(out IJob job))
            {
                if (job is ComplexJob complexJob)
                {
                    HandleComplexObjectJob(complexJob);
                }
                else if (job is CollectionJob collectionJob)
                {
                    HandleCollectionJob(collectionJob);
                }
            }
        }

        private void HandleCollectionJob(CollectionJob job)
        {
            if (job.index == 0)
            {
                PrepareUnexpectedValue(job.deviatingType, job.itemType);
                writer.OpenCollection();
            }

            if (!job.enumerator.MoveNext())
            {
                writer.CloseCollection();
                FinishUnexpectedValue(job.deviatingType);
            }
            else
            {
                if (job.index++ > 0) writer.WriteComma();
                jobStack.Push(job);

                HandleItem(job.enumerator.Current, job.collectionType, settings.referenceCheck != ReferenceCheck.NoRefCheck ? $"{job.currentPath}[{job.index}]" : default);
            }
        }

        private void HandleComplexObjectJob(ComplexJob job)
        {
            if (job.firstChild)
            {
                writer.OpenObject();
                if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && job.deviatingType))
                {
                    writer.WriteTypeInfo(job.itemType.FullName);
                    job.firstChild = false;
                }
            }

            if (!job.enumerator.MoveNext())
            {
                writer.CloseObject();
            }
            else
            {

                if (job.firstChild) job.firstChild = false;
                else writer.WriteComma();

                jobStack.Push(job);

                var fieldWriter = (Action<object, string>)job.enumerator.Current;

                fieldWriter(job.item, job.currentPath);
            }
        }

        private void HandleItem(object obj, Type expectedType, string currentPath)
        {
            if (obj == null)
            {
                writer.WriteNullValue();
                return;
            }

            Type objType = obj.GetType();
            bool deviatingType = expectedType != obj.GetType();

            if (objType.IsPrimitive)
            {
                PrepareUnexpectedValue(deviatingType, objType);
                writer.WritePrimitiveValue(obj);
                FinishUnexpectedValue(deviatingType);
                return;
            }

            if (obj is string str)
            {
                PrepareUnexpectedValue(deviatingType, objType);
                writer.WriteStringValue(str);
                FinishUnexpectedValue(deviatingType);
                return;
            }

            if (settings.referenceCheck != ReferenceCheck.NoRefCheck)
            {
                if (TryHandleRef(obj, currentPath)) return;                
                if (objType.IsClass) pathMap[obj] = currentPath;
            }

            if (!typeCache.TryGetValue(objType, out var typeCacheItem))
            {
                typeCacheItem = CreateTypeCacheItem(obj, objType);
            }

            if (typeCacheItem.isCollection)
            {
                HandleCollection(currentPath, objType, deviatingType, obj as IEnumerable, typeCacheItem);
                return;
            }

            ComplexJob job = new()
            {                                
                firstChild = true,
                item = obj,
                itemType = objType,
                enumerator = typeCacheItem.fieldWriters.GetEnumerator(),
                currentPath = currentPath,
                deviatingType = deviatingType
            };
            jobStack.Push(job);
        }

        private TypeCacheItem CreateTypeCacheItem(object obj, Type objType)
        {
            TypeCacheItem typeCacheItem = new TypeCacheItem();
            if (obj is IEnumerable)
            {
                Type collectionType = objType.GetFirstTypeParamOfGenericInterface(typeof(ICollection<>));
                if (collectionType != null)
                {
                    typeCacheItem.isCollection = true;
                    typeCacheItem.collectionType = collectionType;
                }
                else if (objType is ICollection)
                {
                    typeCacheItem.isCollection = true;
                    typeCacheItem.collectionType = typeof(object);
                }
                else typeCacheItem.isCollection = false;
            }
            else typeCacheItem.isCollection = false;

            if (!typeCacheItem.isCollection)
            {
                var memberInfos = new List<MemberInfo>();
                if (settings.dataSelection == DataSelection.PublicFieldsAndProperties)
                {
                    memberInfos.AddRange(objType.GetFields(BindingFlags.Public | BindingFlags.Instance));
                    memberInfos.AddRange(objType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.GetMethod != null));
                }
                else
                {
                    memberInfos.AddRange(objType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                    Type t = objType.BaseType;
                    while (t != null)
                    {
                        memberInfos.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !memberInfos.Any(field => field.Name == baseField.Name)));
                        t = t.BaseType;
                    }
                }

                typeCacheItem.fieldWriters = new();
                foreach (var memberInfo in memberInfos)
                {
                    var fieldWriter = CreateFieldWriter(objType, memberInfo);
                    typeCacheItem.fieldWriters.Add(fieldWriter);
                }
            }

            typeCache[objType] = typeCacheItem;
            return typeCacheItem;
        }

        private bool TryHandleRef(object obj, string currentPath)
        {
            bool done = false;
            if (pathMap.TryGetValue(obj, out string refPath))
            {
                if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef)
                {
                    writer.WriteRefObject(refPath);
                }
                else if (currentPath.StartsWith(refPath))
                {
                    if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByRef)
                    {
                        writer.WriteRefObject(refPath);
                    }
                    else if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByNull)
                    {
                        writer.WriteNullValue();
                    }
                    else if (settings.referenceCheck == ReferenceCheck.OnLoopThrowException)
                    {
                        throw new Exception("Circular referencing detected!");
                    }
                }
                done = true;
            }

            return done;
        }

        private void HandleCollection(string currentPath, Type objType, bool deviatingType, IEnumerable items, TypeCacheItem typeCacheItem)
        {
            if (items is ICollection<string> string_items)
            {
                PrepareUnexpectedValue(deviatingType, objType);
                SerializeStringCollection(string_items);
                FinishUnexpectedValue(deviatingType);
            }
            else if (typeCacheItem.collectionType.IsPrimitive)
            {
                PrepareUnexpectedValue(deviatingType, objType);
                if (items is ICollection<int> int_items) SerializePrimitiveCollection(int_items);
                else if (items is ICollection<uint> uint_items) SerializePrimitiveCollection(uint_items);
                else if (items is ICollection<byte> byte_items) SerializePrimitiveCollection(byte_items);
                else if (items is ICollection<sbyte> sbyte_items) SerializePrimitiveCollection(sbyte_items);
                else if (items is ICollection<short> short_items) SerializePrimitiveCollection(short_items);
                else if (items is ICollection<ushort> ushort_items) SerializePrimitiveCollection(ushort_items);
                else if (items is ICollection<long> long_items) SerializePrimitiveCollection(long_items);
                else if (items is ICollection<ulong> ulong_items) SerializePrimitiveCollection(ulong_items);
                else if (items is ICollection<bool> bool_items) SerializePrimitiveCollection(bool_items);
                else if (items is ICollection<char> char_items) SerializePrimitiveCollection(char_items);
                else if (items is ICollection<float> float_items) SerializePrimitiveCollection(float_items);
                else if (items is ICollection<double> double_items) SerializePrimitiveCollection(double_items);
                else if (items is ICollection<IntPtr> intPtr_items) SerializePrimitiveCollection(intPtr_items);
                else if (items is ICollection<UIntPtr> uIntPtr_items) SerializePrimitiveCollection(uIntPtr_items);
                FinishUnexpectedValue(deviatingType);
            }
            else
            {
                CollectionJob job = new()
                {
                    collectionType = typeCacheItem.collectionType,
                    index = 0,
                    item = items,
                    itemType = objType,
                    enumerator = items.GetEnumerator(),
                    currentPath = currentPath,
                    deviatingType = deviatingType
                };
                jobStack.Push(job);
            }
        }

        private void SerializeStringCollection(ICollection<string> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteStringValue(item);
            }
            writer.CloseCollection();
        }

        private void SerializePrimitiveCollection<T>(ICollection<T> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WritePrimitiveValue(item);
            }
            writer.CloseCollection();
        }

        private Action<object, string> CreateFieldWriter(Type objType, MemberInfo memberInfo)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;
            Func<object?, object?> getValue = memberInfo is FieldInfo field2 ? field2.GetValue : memberInfo is PropertyInfo property2 ? property2.GetValue : default;

            string fieldName = memberInfo.Name;
            if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                memberInfo.Name.StartsWith('<') &&
                memberInfo.Name.EndsWith(">k__BackingField"))
            {
                fieldName = fieldName.Substring("<", ">");
            }

            if (memberType == typeof(string)) return CreateStringFieldWriter(objType, memberInfo, fieldName);
            else if (memberType == typeof(int)) return CreatePrimitiveFieldWriter<int>(objType, memberInfo, fieldName);
            else if (memberType == typeof(uint)) return CreatePrimitiveFieldWriter<uint>(objType, memberInfo, fieldName);
            else if (memberType == typeof(byte)) return CreatePrimitiveFieldWriter<byte>(objType, memberInfo, fieldName);
            else if (memberType == typeof(sbyte)) return CreatePrimitiveFieldWriter<sbyte>(objType, memberInfo, fieldName);
            else if (memberType == typeof(short)) return CreatePrimitiveFieldWriter<short>(objType, memberInfo, fieldName);
            else if (memberType == typeof(ushort)) return CreatePrimitiveFieldWriter<ushort>(objType, memberInfo, fieldName);
            else if (memberType == typeof(long)) return CreatePrimitiveFieldWriter<long>(objType, memberInfo, fieldName);
            else if (memberType == typeof(ulong)) return CreatePrimitiveFieldWriter<ulong>(objType, memberInfo, fieldName);
            else if (memberType == typeof(bool)) return CreatePrimitiveFieldWriter<bool>(objType, memberInfo, fieldName);
            else if (memberType == typeof(char)) return CreatePrimitiveFieldWriter<char>(objType, memberInfo, fieldName);
            else if (memberType == typeof(float)) return CreatePrimitiveFieldWriter<float>(objType, memberInfo, fieldName);
            else if (memberType == typeof(double)) return CreatePrimitiveFieldWriter<double>(objType, memberInfo, fieldName);
            else if (memberType == typeof(IntPtr)) return CreatePrimitiveFieldWriter<IntPtr>(objType, memberInfo, fieldName);
            else if (memberType == typeof(UIntPtr)) return CreatePrimitiveFieldWriter<UIntPtr>(objType, memberInfo, fieldName);
            else if (memberType.IsAssignableTo(typeof(IEnumerable)) &&
                        (memberType.IsAssignableTo(typeof(ICollection)) ||
                         memberType.IsOfGenericType(typeof(ICollection<>))))
            {

                return CreateCollectionFieldWriter(objType, memberInfo, fieldName);
            }

            return (obj, currentPath) =>
            {                
                writer.WriteFieldName(fieldName);
                var value = getValue(obj);
                HandleItem(value, memberType, settings.referenceCheck != ReferenceCheck.NoRefCheck ? $"{currentPath}.{fieldName}" : null);
            };
        }

        private Action<object, string> CreateStringFieldWriter(Type objType, MemberInfo memberInfo, string fieldName)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, string>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (obj, currentPath) =>
            {
                var value = getValue(obj);                
                writer.WriteFieldName(fieldName);
                if (value == null) writer.WriteNullValue();
                else writer.WriteStringValue(value);                
            };
        }

        private Action<object, string> CreatePrimitiveFieldWriter<T>(Type objType, MemberInfo memberInfo, string fieldName)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, T>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (obj, currentPath) =>
            {
                var value = getValue(obj);
                writer.WriteFieldName(fieldName);
                if (value == null) writer.WriteNullValue();
                else writer.WritePrimitiveValue(value);
            };
        }

        private Action<object, string> CreateCollectionFieldWriter(Type objType, MemberInfo memberInfo, string fieldName)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;
            Type collectionType = memberInfo is FieldInfo field2 ? field2.FieldType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  memberInfo is PropertyInfo property2 ? property2.PropertyType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  default;
            collectionType = collectionType ?? typeof(object);

            if (collectionType == typeof(string)) return CreateStringCollectionFieldWriter(objType, memberInfo, fieldName);
            else if (collectionType == typeof(int)) return CreatePrimitiveCollectionFieldWriter<int>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(uint)) return CreatePrimitiveCollectionFieldWriter<uint>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(byte)) return CreatePrimitiveCollectionFieldWriter<byte>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(sbyte)) return CreatePrimitiveCollectionFieldWriter<sbyte>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(short)) return CreatePrimitiveCollectionFieldWriter<short>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(ushort)) return CreatePrimitiveCollectionFieldWriter<ushort>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(long)) return CreatePrimitiveCollectionFieldWriter<long>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(ulong)) return CreatePrimitiveCollectionFieldWriter<ulong>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(bool)) return CreatePrimitiveCollectionFieldWriter<bool>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(char)) return CreatePrimitiveCollectionFieldWriter<char>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(float)) return CreatePrimitiveCollectionFieldWriter<float>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(double)) return CreatePrimitiveCollectionFieldWriter<double>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(IntPtr)) return CreatePrimitiveCollectionFieldWriter<IntPtr>(objType, memberInfo, fieldName);
            else if (collectionType == typeof(UIntPtr)) return CreatePrimitiveCollectionFieldWriter<UIntPtr>(objType, memberInfo, fieldName);

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = memberInfo is FieldInfo field3 ? field3.DeclaringType : memberInfo is PropertyInfo property3 ? property3.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = memberInfo is FieldInfo field4 ? Expression.Field(castedParameter, field4) : memberInfo is PropertyInfo property4 ? Expression.Property(castedParameter, property4) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable));
            var lambda = Expression.Lambda<Func<object, IEnumerable>>(castFieldAccess, parameter);
            var getValue = lambda.Compile();

            return (obj, currentPath) =>
            {                
                IEnumerable items = getValue(obj);
                writer.WriteFieldName(fieldName);
                CollectionJob job = new()
                {
                    collectionType = collectionType,
                    index = 0,
                    item = items,
                    itemType = objType,
                    enumerator = items.GetEnumerator(),
                    currentPath = currentPath,
                    deviatingType = objType != memberType
                };
                jobStack.Push(job);
            };
        }

        private Action<object, string> CreateStringCollectionFieldWriter(Type objType, MemberInfo memberInfo, string fieldName)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = memberInfo is FieldInfo field2 ? field2.DeclaringType : memberInfo is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = memberInfo is FieldInfo field3 ? Expression.Field(castedParameter, field3) : memberInfo is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<string>));
            var lambda = Expression.Lambda<Func<object, IEnumerable<string>>>(castFieldAccess, parameter);
            var getValue = lambda.Compile();

            return (obj, currentPath) =>
            {
                Type objType = obj.GetType();
                bool deviating = memberType != objType;

                IEnumerable<string> items = getValue(obj);
                writer.WriteFieldName(fieldName);
                PrepareUnexpectedValue(deviating, objType);
                writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else writer.WriteComma();
                    writer.WriteStringValue(item);
                }
                writer.CloseCollection();
                FinishUnexpectedValue(deviating);
            };
        }

        private Action<object, string> CreatePrimitiveCollectionFieldWriter<T>(Type objType, MemberInfo memberInfo, string fieldName)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = memberInfo is FieldInfo field2 ? field2.DeclaringType : memberInfo is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = memberInfo is FieldInfo field3 ? Expression.Field(castedParameter, field3) : memberInfo is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<T>));
            var lambda = Expression.Lambda<Func<object, IEnumerable<T>>>(castFieldAccess, parameter);
            var getValue = lambda.Compile();

            return (obj, currentPath) =>
            {
                Type objType = obj.GetType();
                bool deviating = memberType != objType;

                IEnumerable<T> items = getValue(obj);
                writer.WriteFieldName(fieldName);
                PrepareUnexpectedValue(deviating, objType);
                writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else writer.WriteComma();
                    writer.WritePrimitiveValue(item);
                }
                writer.CloseCollection();
                FinishUnexpectedValue(deviating);
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void PrepareUnexpectedValue(bool deviatingType, Type objType)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
            {
                writer.OpenObject();
                writer.WriteTypeInfo(objType.FullName);
                writer.WriteComma();
                writer.WriteValueFieldName();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FinishUnexpectedValue(bool deviatingType)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && deviatingType))
            {
                writer.CloseObject();
            }
        }


    }

}
