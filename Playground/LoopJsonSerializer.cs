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
using System.Net.Http.Headers;
using FeatureLoom.Synchronization;

namespace Playground
{
    public sealed partial class LoopJsonSerializer
    {

        delegate void FieldWriter(ComplexJob parentJob);
        delegate void ItemHandler(object obj, BaseJob parentJob, bool writeTypeInfo);

        abstract class BaseJob
        {
            public object item;
            public Type itemType;
            public IEnumerator enumerator;
            public bool writeTypeInfo;
            public BaseJob parentJob;
            public byte[] itemName;
        }

        sealed class CollectionJob : BaseJob
        {
            public Type collectionType;
            public int index;
            internal TypeCacheItem collectionTypeCacheItem;
        }

        sealed class ComplexJob : BaseJob
        {
            public bool firstChild;
            public byte[] currentFieldName;
        }

        class TypeCacheItem
        {
            public ItemHandler itemHandler;
        }

        struct EnumKey
        {
            public Type enumType;
            public int value;

            public EnumKey(object enumValue, Type enumType)
            {
                this.enumType = enumType;
                this.value = (int)enumValue;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(enumType, value);
            }

            public static implicit operator EnumKey((object enumValue, Type enumType) pair) => new EnumKey(pair.enumValue, pair.enumType);
        }

        FeatureLock serializerLock = new FeatureLock();
        Stack<BaseJob> jobStack = new Stack<BaseJob>();
        Settings settings;
        JsonUTF8StreamWriter writer = new JsonUTF8StreamWriter();
        MemoryStream memoryStream = new MemoryStream();
        Dictionary<object, BaseJob> objToJob = new();
        Dictionary<Type, TypeCacheItem> typeCache = new();
        Dictionary<EnumKey, byte[]> enumTextMap = new();

        readonly Action<bool, byte[]> prepareTypeInfoObjectFromBytes;
        readonly Action<bool, Type> prepareTypeInfoObjectFromType;
        readonly Action<bool> finishTypeInfoObject;
        readonly Func<object, BaseJob, Type, bool> tryHandleAsRef;
            


        public LoopJsonSerializer(Settings settings = null)
        {
            this.settings = settings ?? new();

            prepareTypeInfoObjectFromBytes = PrepareTypeInfoObject;
            prepareTypeInfoObjectFromType = PrepareTypeInfoObject;
            finishTypeInfoObject = FinishTypeInfoObject;
            if (settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
            {
                prepareTypeInfoObjectFromBytes = (_, _) => { };
                prepareTypeInfoObjectFromType = (_, _) => { };
                finishTypeInfoObject = (_) => { };
            }
            tryHandleAsRef = TryHandleAsRef;
            if (settings.referenceCheck == ReferenceCheck.NoRefCheck)
            {
                tryHandleAsRef = (_, _, _) => false;
            }
        }


        public string Serialize<T>(T item)
        {
            using (serializerLock.Lock())
            {
                memoryStream.Position = 0;
                objToJob.Clear();

                writer.stream = memoryStream;
                HandleItem(item, item?.GetType(), typeof(T), null);
                Loop();
                return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            }
        }

        public byte[] SerializeToUtf8Bytes<T>(T item)
        {
            using (serializerLock.Lock())
            {
                memoryStream.Position = 0;
                objToJob.Clear();

                writer.stream = memoryStream;
                HandleItem(item, item?.GetType(), typeof(T), null);
                Loop();
                return memoryStream.ToArray();
            }
        }

        public void Serialize<T>(Stream stream, T item)
        {
            using (serializerLock.Lock())
            {
                objToJob.Clear();

                writer.stream = stream;
                HandleItem(item, item?.GetType(), typeof(T), null);
                Loop();
            }
        }

        private void Loop()
        {
            while (jobStack.TryPop(out BaseJob job))
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
                prepareTypeInfoObjectFromType(job.writeTypeInfo, job.itemType);
                writer.OpenCollection();
            }

            jobStack.Push(job);
            int beforeStackSize = jobStack.Count;
            do
            {
                if (!job.enumerator.MoveNext())
                {
                    writer.CloseCollection();
                    finishTypeInfoObject(job.writeTypeInfo);
                    jobStack.Pop();
                    return;
                }
            
                if (job.index > 0) writer.WriteComma();
                
                var item = job.enumerator.Current;
                var itemType = item?.GetType();
                if (job.collectionType == itemType) job.collectionTypeCacheItem.itemHandler(item, job, settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo);
                else HandleItem(item, itemType, job.collectionType, job);                

                job.index++;            
            } while (jobStack.Count == beforeStackSize);
        }

        private void HandleComplexObjectJob(ComplexJob job)
        {
            if (job.firstChild)
            {
                writer.OpenObject();
                if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && job.writeTypeInfo))
                {
                    writer.WriteTypeInfo(job.itemType.GetSimplifiedTypeName());
                    job.firstChild = false;
                }
            }

            jobStack.Push(job);
            int beforeStackSize = jobStack.Count;
            do
            {
                if (!job.enumerator.MoveNext())
                {
                    writer.CloseObject();
                    jobStack.Pop();
                    return;
                }                

                if (job.firstChild) job.firstChild = false;
                else writer.WriteComma();

                var fieldWriter = (FieldWriter)job.enumerator.Current;                
                fieldWriter(job);
            } while (jobStack.Count == beforeStackSize);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleItem(object obj, Type objType, Type expectedType, BaseJob parentJob)
        {
            if (obj == null)
            {
                writer.WriteNullValue();
                return;
            }
            bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && expectedType != objType);
            
            if (!typeCache.TryGetValue(objType, out var typeCacheItem))
            {
                typeCacheItem = CreateTypeCacheItem(objType);
            }            
            typeCacheItem.itemHandler(obj, parentJob, writeTypeInfo);                        
        }

        private bool TryHandleAsRef(object obj, BaseJob parentJob, Type objType)
        {
            if (parentJob == null || !objType.IsClass || settings.referenceCheck == ReferenceCheck.NoRefCheck) return false;

            if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef)
            {
                if (objToJob.TryGetValue(obj, out BaseJob refJob))
                {
                    writer.WriteRefObject(refJob);
                    return true;
                }
            }
            else if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByRef)
            {
                foreach (var refJob in jobStack)
                {
                    if (refJob.item == obj)
                    {
                        writer.WriteRefObject(refJob);
                        return true;
                    }
                }
            }
            else if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByNull)
            {
                foreach (var refJob in jobStack)
                {
                    if (refJob.item == obj)
                    {
                        writer.WriteNullValue();
                        return true;
                    }
                }
            }
            else if (settings.referenceCheck == ReferenceCheck.OnLoopThrowException)
            {
                foreach (var refJob in jobStack)
                {
                    if (refJob.item == obj)
                    {
                        throw new Exception("Circular referencing detected!"); ;
                    }
                }
            }
            return false;
        }

        private TypeCacheItem CreateTypeCacheItem(Type objType)
        {
            TypeCacheItem typeCacheItem = new TypeCacheItem();
            typeCache[objType] = typeCacheItem;
            byte[] preparedTypeInfo = writer.PrepareTypeInfo(objType.GetSimplifiedTypeName());            


            if (objType == typeof(string))
            {
                typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    writer.WriteStringValue((string)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                return typeCacheItem;
            }

            if (objType.IsPrimitive)
            {
                if (objType == typeof(int) || objType == typeof(short) || objType == typeof(sbyte))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteSignedIntValue((int)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (objType == typeof(long))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteSignedIntValue((long)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (objType == typeof(uint) || objType == typeof(ushort) || objType == typeof(byte))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteUnsignedIntValue((uint)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (objType == typeof(ulong))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteUnsignedIntValue((ulong)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (objType == typeof(bool))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteBoolValue((bool)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (objType == typeof(double))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteFloatValue((double)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (objType == typeof(float))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteFloatValue((float)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WritePrimitiveValue(obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                return typeCacheItem;
            }

            if (objType.IsEnum)
            {
                if (settings.enumAsString)
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WritePreparedByteString(GetEnumText(obj, objType));
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        writer.WriteSignedIntValue((int)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                return typeCacheItem;
            }

            if (objType.IsAssignableTo(typeof(IEnumerable)))
            {
                Type collectionType = objType.GetFirstTypeParamOfGenericInterface(typeof(ICollection<>));

                if (collectionType == typeof(string))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<string>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(int))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<int>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(uint))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<uint>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(byte))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<byte>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(sbyte))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<sbyte>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(short))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<short>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(ushort))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<ushort>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(long))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<long>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(ulong))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<ulong>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(bool))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<bool>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(char))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<char>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(float))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<float>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(double))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<double>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(IntPtr))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<IntPtr>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (collectionType == typeof(UIntPtr))
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<UIntPtr>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                }
                else if (objType.IsAssignableTo(typeof(ICollection)))
                {
                    if (collectionType == null) collectionType = typeof(object);

                    if (!typeCache.TryGetValue(collectionType, out var collectionTypeCacheItem))
                    {
                        collectionTypeCacheItem = CreateTypeCacheItem(collectionType);
                    }

                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        var items = (IEnumerable)obj;
                        CollectionJob job = new()
                        {
                            collectionType = collectionType,
                            index = 0,
                            item = obj,
                            itemType = objType,
                            enumerator = items.GetEnumerator(),
                            collectionTypeCacheItem = collectionTypeCacheItem,
                            writeTypeInfo = writeTypeInfo,
                            parentJob = parentJob,
                            itemName = parentJob == null ? writer.PrepareRootName() :
                                        parentJob is ComplexJob complexParentJob ? complexParentJob.currentFieldName :
                                        writer.PrepareCollectionIndexName((CollectionJob)parentJob)

                        };
                        jobStack.Push(job);
                        if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef && objType.IsClass) objToJob[items] = job;
                    };
                }
                if (typeCacheItem.itemHandler != null) return typeCacheItem;
            }

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

                List<FieldWriter> fieldWriters = new();
                foreach (var memberInfo in memberInfos)
                {
                    var fieldWriter = CreateFieldWriter(objType, memberInfo);
                    fieldWriters.Add(fieldWriter);
                }

                if (fieldWriters.Count > 0)
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;

                        ComplexJob job = new()
                        {
                            firstChild = true,
                            item = obj,
                            itemType = objType,
                            enumerator = fieldWriters.GetEnumerator(),
                            writeTypeInfo = writeTypeInfo,
                            parentJob = parentJob,
                            itemName = parentJob == null ? writer.PrepareRootName() :
                                       parentJob is ComplexJob complexParentJob ? complexParentJob.currentFieldName :
                                       writer.PrepareCollectionIndexName((CollectionJob)parentJob)
                        };
                        jobStack.Push(job);
                        if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef && objType.IsClass) objToJob[obj] = job;
                    };
                }
                else
                {
                    typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        writer.OpenObject();
                        if (writeTypeInfo) writer.WritePreparedByteString(preparedTypeInfo);
                        writer.CloseCollection();
                    };
                }

            }
            return typeCacheItem;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<string> items)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<int> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteSignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<sbyte> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteSignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<short> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteSignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<long> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteSignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<byte> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteUnsignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<uint> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteUnsignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<ushort> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteUnsignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<ulong> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteUnsignedIntValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<float> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteFloatValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<double> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteFloatValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<bool> items)
        {
            writer.OpenCollection();
            bool isFirstItem = true;
            foreach (var item in items)
            {
                if (isFirstItem) isFirstItem = false;
                else writer.WriteComma();
                writer.WriteBoolValue(item);
            }
            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        private FieldWriter CreateFieldWriter(Type objType, MemberInfo memberInfo)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;
            Func<object, object> getValue = memberInfo is FieldInfo field2 ? field2.GetValue : memberInfo is PropertyInfo property2 ? property2.GetValue : default;

            string fieldName = memberInfo.Name;
            if (settings.dataSelection == DataSelection.PublicAndPrivateFields_CleanBackingFields &&
                memberInfo.Name.StartsWith('<') &&
                memberInfo.Name.EndsWith(">k__BackingField"))
            {
                fieldName = fieldName.Substring("<", ">");
            }

            var extendedFieldNameBytes = writer.PrepareFieldNameBytes(fieldName);
            var fieldNameBytes = writer.PrepareStringToBytes(fieldName);

            if (memberType == typeof(string)) return CreateStringFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(int)) return CreateIntFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(uint)) return CreateUintFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(byte)) return CreateByteFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(sbyte)) return CreateSbyteFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(short)) return CreateShortFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(ushort)) return CreateUshortFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(long)) return CreateLongFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(ulong)) return CreateUlongFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(bool)) return CreateBoolFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(char)) return CreatePrimitiveFieldWriter<char>(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(float)) return CreateFloatFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(double)) return CreateDoubleFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(IntPtr)) return CreatePrimitiveFieldWriter<IntPtr>(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType == typeof(UIntPtr)) return CreatePrimitiveFieldWriter<UIntPtr>(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType.IsEnum) return CreateEnumFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (memberType.IsAssignableTo(typeof(IEnumerable)) &&
                        (memberType.IsAssignableTo(typeof(ICollection)) ||
                         memberType.IsOfGenericType(typeof(ICollection<>))))
            {

                return CreateCollectionFieldWriter(objType, memberInfo, extendedFieldNameBytes, fieldNameBytes);
            }

            if (!typeCache.TryGetValue(memberType, out var memberTypeCacheItem))
            {
                memberTypeCacheItem = CreateTypeCacheItem(memberType);
            }            

            return (parentJob) =>
            {
                parentJob.currentFieldName = fieldNameBytes;
                writer.WritePreparedByteString(extendedFieldNameBytes);
                var value = getValue(parentJob.item);
                var valueType = value?.GetType();
                if (valueType == memberType)
                {
                    bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo;
                    memberTypeCacheItem.itemHandler(value, parentJob, writeTypeInfo);
                }
                else HandleItem(value, valueType, memberType, parentJob);
            };
        }

        private FieldWriter CreateStringFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, string>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {                
                writer.WritePreparedByteString(fieldNameBytes);
                var value = getValue(parentJob.item);
                if (value == null) writer.WriteNullValue();
                else writer.WriteStringValue(value);
            };
        }

        private FieldWriter CreatePrimitiveFieldWriter<T>(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, T>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WritePrimitiveValue(value);
            };
        }

        private FieldWriter CreateBoolFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, bool>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteBoolValue(value);
            };
        }

        private FieldWriter CreateIntFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, int>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                writer.WritePreparedByteString(fieldNameBytes);
                var value = getValue(parentJob.item);                
                writer.WriteSignedIntValue(value);
            };
        }

        private FieldWriter CreateSbyteFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, sbyte>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteSignedIntValue(value);
            };
        }

        private FieldWriter CreateShortFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, short>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteSignedIntValue(value);
            };
        }

        private FieldWriter CreateLongFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, long>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteSignedIntValue(value);
            };
        }

        private FieldWriter CreateUintFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, uint>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteUnsignedIntValue(value);
            };
        }

        private FieldWriter CreateByteFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, byte>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteUnsignedIntValue(value);
            };
        }

        private FieldWriter CreateUshortFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, ushort>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteUnsignedIntValue(value);
            };
        }

        private FieldWriter CreateUlongFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, ulong>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteUnsignedIntValue(value);
            };
        }

        private FieldWriter CreateFloatFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, float>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteFloatValue(value);
            };
        }

        private FieldWriter CreateDoubleFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, double>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                writer.WriteFloatValue(value);
            };
        }

        private FieldWriter CreateEnumFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            Func<object, object> getValue = memberInfo is FieldInfo fieldInfo ? fieldInfo.GetValue : memberInfo is PropertyInfo propertyInfo ? propertyInfo.GetValue : default;

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                if (settings.enumAsString) writer.WritePreparedByteString(GetEnumText(value, objType));
                else writer.WriteSignedIntValue((int)value);
            };
        }

        private FieldWriter CreateCollectionFieldWriter(Type objType, MemberInfo memberInfo, byte[] extendedFieldNameBytes, byte[] fieldNameBytes)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;
            Type collectionType = memberInfo is FieldInfo field2 ? field2.FieldType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  memberInfo is PropertyInfo property2 ? property2.PropertyType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  default;
            collectionType = collectionType ?? typeof(object);

            if (collectionType == typeof(string)) return CreateStringCollectionFieldWriter(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(int)) return CreatePrimitiveCollectionFieldWriter<int>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(uint)) return CreatePrimitiveCollectionFieldWriter<uint>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(byte)) return CreatePrimitiveCollectionFieldWriter<byte>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(sbyte)) return CreatePrimitiveCollectionFieldWriter<sbyte>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(short)) return CreatePrimitiveCollectionFieldWriter<short>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(ushort)) return CreatePrimitiveCollectionFieldWriter<ushort>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(long)) return CreatePrimitiveCollectionFieldWriter<long>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(ulong)) return CreatePrimitiveCollectionFieldWriter<ulong>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(bool)) return CreatePrimitiveCollectionFieldWriter<bool>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(char)) return CreatePrimitiveCollectionFieldWriter<char>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(float)) return CreatePrimitiveCollectionFieldWriter<float>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(double)) return CreatePrimitiveCollectionFieldWriter<double>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(IntPtr)) return CreatePrimitiveCollectionFieldWriter<IntPtr>(objType, memberInfo, extendedFieldNameBytes);
            else if (collectionType == typeof(UIntPtr)) return CreatePrimitiveCollectionFieldWriter<UIntPtr>(objType, memberInfo, extendedFieldNameBytes);

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = memberInfo is FieldInfo field3 ? field3.DeclaringType : memberInfo is PropertyInfo property3 ? property3.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = memberInfo is FieldInfo field4 ? Expression.Field(castedParameter, field4) : memberInfo is PropertyInfo property4 ? Expression.Property(castedParameter, property4) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable));
            var lambda = Expression.Lambda<Func<object, IEnumerable>>(castFieldAccess, parameter);
            var getValue = lambda.Compile();

            if (!typeCache.TryGetValue(collectionType, out var collectionTypeCacheItem))
            {
                collectionTypeCacheItem = CreateTypeCacheItem(collectionType);
            }

            return (parentJob) =>
            {
                IEnumerable items = getValue(parentJob.item);
                Type objType = items.GetType();
                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && memberType != objType);
                writer.WritePreparedByteString(extendedFieldNameBytes);
                CollectionJob job = new()
                {
                    collectionType = collectionType,
                    index = 0,
                    item = items,
                    itemType = objType,
                    enumerator = items.GetEnumerator(),
                    collectionTypeCacheItem = collectionTypeCacheItem,
                    writeTypeInfo = writeTypeInfo,
                    parentJob = parentJob,
                    itemName = fieldNameBytes
                };
                jobStack.Push(job);
                if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef && objType.IsClass) objToJob[items] = job;
            };
        }

        private FieldWriter CreateStringCollectionFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = memberInfo is FieldInfo field2 ? field2.DeclaringType : memberInfo is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = memberInfo is FieldInfo field3 ? Expression.Field(castedParameter, field3) : memberInfo is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<string>));
            var lambda = Expression.Lambda<Func<object, IEnumerable<string>>>(castFieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                IEnumerable<string> items = getValue(parentJob.item);
                Type objType = items.GetType();
                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && memberType != objType);
                writer.WritePreparedByteString(fieldNameBytes);
                prepareTypeInfoObjectFromType(writeTypeInfo, objType);
                writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else writer.WriteComma();
                    writer.WriteStringValue(item);
                }
                writer.CloseCollection();
                finishTypeInfoObject(writeTypeInfo);
            };
        }

        private FieldWriter CreatePrimitiveCollectionFieldWriter<T>(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = memberInfo is FieldInfo field2 ? field2.DeclaringType : memberInfo is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = memberInfo is FieldInfo field3 ? Expression.Field(castedParameter, field3) : memberInfo is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IEnumerable<T>));
            var lambda = Expression.Lambda<Func<object, IEnumerable<T>>>(castFieldAccess, parameter);
            var getValue = lambda.Compile();

            Action<T> writeValue = writer.WritePrimitiveValue;
            if (typeof(T).IsAssignableTo(typeof(int))) writeValue = value => { if (value is int v) writer.WriteSignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(uint))) writeValue = value => { if (value is uint v) writer.WriteUnsignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(long))) writeValue = value => { if (value is long v) writer.WriteSignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(ulong))) writeValue = value => { if (value is ulong v) writer.WriteUnsignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(float))) writeValue = value => { if (value is float v) writer.WriteFloatValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(double))) writeValue = value => { if (value is double v) writer.WriteFloatValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(bool))) writeValue = value => { if (value is bool v) writer.WriteBoolValue(v); };

            return (parentJob) =>
            {
                IEnumerable<T> items = getValue(parentJob.item);
                Type objType = items.GetType();
                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && memberType != objType);
                writer.WritePreparedByteString(fieldNameBytes);
                prepareTypeInfoObjectFromType(writeTypeInfo, objType);
                writer.OpenCollection();
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (isFirstItem) isFirstItem = false;
                    else writer.WriteComma();
                    writeValue(item);
                }
                writer.CloseCollection();
                finishTypeInfoObject(writeTypeInfo);
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void PrepareTypeInfoObject(bool write, Type objType)
        {
            if (!write) return;

            writer.OpenObject();
            writer.WriteTypeInfo(objType.GetSimplifiedTypeName());
            writer.WriteComma();
            writer.WriteValueFieldName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void PrepareTypeInfoObject(bool write, byte[] preparedTypeInfo)
        {
            if (!write) return;

            writer.OpenObject();
            writer.WritePreparedByteString(preparedTypeInfo);
            writer.WriteComma();
            writer.WriteValueFieldName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FinishTypeInfoObject(bool write)
        {
            if (!write) return;

            writer.CloseObject();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        byte[] GetEnumText(object enumValue, Type enumType)
        {
            EnumKey key = (enumValue, enumType);
            if (!enumTextMap.TryGetValue(key, out byte[] text))
            {
                text = writer.PrepareEnumTextToBytes(enumValue.ToString());
                enumTextMap[key] = text;
            }
            return text;
        }

    }

}
