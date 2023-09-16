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
            public bool writeTypeInfo;
            public BaseJob parentJob;
            public byte[] itemName;
            public int currentIndex;
        }

        sealed class EnumarableJob : BaseJob
        {
            public IEnumerator enumerator;
            public Type collectionType;            
            internal TypeCacheItem collectionTypeCacheItem;
        }

        sealed class ListJob : BaseJob
        {
            public Type collectionType;
            internal TypeCacheItem collectionTypeCacheItem;
        }

        sealed class ComplexJob : BaseJob
        {
            public List<FieldWriter> fieldWriters;
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
                else if (job is ListJob listJob)
                {
                    HandleListJob(listJob);
                }
                else if (job is EnumarableJob enumerableJob)
                {                    
                    HandleEnumerableJob(enumerableJob);
                }
            }
        }

        private void HandleEnumerableJob(EnumarableJob job)
        {
            jobStack.Push(job);
            int beforeStackSize = jobStack.Count;

            if (job.currentIndex == 0)
            {
                prepareTypeInfoObjectFromType(job.writeTypeInfo, job.itemType);
                writer.OpenCollection();

                if (job.enumerator.MoveNext())
                {
                    bool stackChanged = HandleNextCollectionItem(job, beforeStackSize);
                    if (stackChanged) return;
                }
            }            

            while (job.enumerator.MoveNext())
            {
                writer.WriteComma();
                bool stackChanged = HandleNextCollectionItem(job, beforeStackSize);
                if (stackChanged) return;
            }

            writer.CloseCollection();
            finishTypeInfoObject(job.writeTypeInfo);
            jobStack.Pop();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool HandleNextCollectionItem(EnumarableJob job, int beforeStackSize)
            {
                var item = job.enumerator.Current;
                var itemType = item?.GetType();
                if (job.collectionType == itemType) job.collectionTypeCacheItem.itemHandler(item, job, settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo);
                else HandleItem(item, itemType, job.collectionType, job);

                job.currentIndex++;

                bool stackChanged = jobStack.Count != beforeStackSize;
                return stackChanged;
            }
        }

        private void HandleListJob(ListJob job)
        {
            jobStack.Push(job);
            int beforeStackSize = jobStack.Count;
            IList list = (IList)job.item;
            if (job.currentIndex == 0)
            {
                prepareTypeInfoObjectFromType(job.writeTypeInfo, job.itemType);
                writer.OpenCollection();

                if (job.currentIndex < list.Count)
                {
                    bool stackChanged = HandleNextCollectionItem(job, beforeStackSize);
                    if (stackChanged) return;
                }
            }

            while (job.currentIndex < list.Count)
            {
                writer.WriteComma();
                bool stackChanged = HandleNextCollectionItem(job, beforeStackSize);
                if (stackChanged) return;
            }

            writer.CloseCollection();
            finishTypeInfoObject(job.writeTypeInfo);
            jobStack.Pop();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool HandleNextCollectionItem(ListJob job, int beforeStackSize)
            {
                IList list = (IList)job.item;
                var item = list[job.currentIndex];
                var itemType = item?.GetType();
                if (job.collectionType == itemType) job.collectionTypeCacheItem.itemHandler(item, job, settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo);
                else HandleItem(item, itemType, job.collectionType, job);

                job.currentIndex++;

                bool stackChanged = jobStack.Count != beforeStackSize;
                return stackChanged;
            }
        }

        private void HandleComplexObjectJob(ComplexJob job)
        {
            jobStack.Push(job);
            int beforeStackSize = jobStack.Count;

            if (job.firstChild)
            {
                job.firstChild = false;
                writer.OpenObject();

                if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && job.writeTypeInfo))
                {
                    writer.WriteTypeInfo(job.itemType.GetSimplifiedTypeName());
                    writer.WriteComma();                    
                }

                if (job.currentIndex < job.fieldWriters.Count)
                {
                    var fieldWriter = job.fieldWriters[job.currentIndex++];
                    fieldWriter(job);
                    if (jobStack.Count != beforeStackSize) return;
                }
            }

            while (job.currentIndex < job.fieldWriters.Count)
            {
                writer.WriteComma();
                var fieldWriter = job.fieldWriters[job.currentIndex++];
                fieldWriter(job);
                if (jobStack.Count != beforeStackSize) return;
            }

            writer.CloseObject();
            jobStack.Pop();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryHandleAsRef(object obj, BaseJob parentJob, Type objType)
        {
            if (!objType.IsClass || parentJob == null) return false;

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
                return CreateTypeCacheItem_String(typeCacheItem, preparedTypeInfo);
            }

            if (objType.IsPrimitive)
            {
                return CreateTypeCacheItem_Primitive(objType, typeCacheItem, preparedTypeInfo);
            }

            if (objType.IsEnum)
            {
                return CreateTypeCacheItem_Enum(objType, typeCacheItem, preparedTypeInfo);
            }

            if (objType.IsAssignableTo(typeof(IEnumerable)))
            {
                if (TryCreateTypeCacheItem_Collection(objType, typeCacheItem, preparedTypeInfo)) return typeCacheItem;
            }

            return CreateTypeCacheItem_Complex(objType, typeCacheItem, preparedTypeInfo);
        }

        private TypeCacheItem CreateTypeCacheItem_Complex(Type objType, TypeCacheItem typeCacheItem, byte[] preparedTypeInfo)
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
                        fieldWriters = fieldWriters,
                        currentIndex = 0,
                        writeTypeInfo = writeTypeInfo,
                        parentJob = parentJob,
                        itemName = parentJob == null ? writer.PrepareRootName() :
                                    parentJob is ComplexJob complexParentJob ? complexParentJob.currentFieldName :
                                    writer.PrepareCollectionIndexName((BaseJob)parentJob)
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
            return typeCacheItem;
        }

        private bool TryCreateTypeCacheItem_Collection(Type objType, TypeCacheItem typeCacheItem, byte[] preparedTypeInfo)
        {
            Type collectionType = objType.GetFirstTypeParamOfGenericInterface(typeof(ICollection<>));

            if (collectionType == typeof(string))
            {
                if (objType.IsAssignableTo(typeof(IList<string>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveList((IList<string>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                    {
                        if (tryHandleAsRef(obj, parentJob, objType)) return;
                        prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                        SerializePrimitiveCollection((ICollection<string>)obj);
                        finishTypeInfoObject(writeTypeInfo);
                    };
            }
            else if (collectionType == typeof(int))
            {
                if (objType.IsAssignableTo(typeof(IList<int>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<int>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<int>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(uint))
            {
                if (objType.IsAssignableTo(typeof(IList<uint>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<uint>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<uint>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(byte))
            {
                if (objType.IsAssignableTo(typeof(IList<byte>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<byte>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<byte>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(sbyte))
            {
                if (objType.IsAssignableTo(typeof(IList<sbyte>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<sbyte>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<sbyte>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(short))
            {
                if (objType.IsAssignableTo(typeof(IList<short>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<short>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<short>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(ushort))
            {
                if (objType.IsAssignableTo(typeof(IList<ushort>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<ushort>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<ushort>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(long))
            {
                if (objType.IsAssignableTo(typeof(IList<long>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<long>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<long>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(ulong))
            {
                if (objType.IsAssignableTo(typeof(IList<ulong>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<ulong>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<ulong>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(bool))
            {
                if (objType.IsAssignableTo(typeof(IList<bool>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<bool>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<bool>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(char))
            {
                if (objType.IsAssignableTo(typeof(IList<char>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<char>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<char>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(float))
            {
                if (objType.IsAssignableTo(typeof(IList<float>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<float>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<float>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(double))
            {
                if (objType.IsAssignableTo(typeof(IList<double>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<double>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<double>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(IntPtr))
            {
                if (objType.IsAssignableTo(typeof(IList<IntPtr>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<IntPtr>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveCollection((ICollection<IntPtr>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
            }
            else if (collectionType == typeof(UIntPtr))
            {
                if (objType.IsAssignableTo(typeof(IList<UIntPtr>))) typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
                {
                    if (tryHandleAsRef(obj, parentJob, objType)) return;
                    prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                    SerializePrimitiveList((IList<UIntPtr>)obj);
                    finishTypeInfoObject(writeTypeInfo);
                };
                else typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
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
                    if (obj == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    if (tryHandleAsRef(obj, parentJob, objType)) return;

                    if (obj is IList list)
                    {
                        ListJob job = new()
                        {
                            collectionType = collectionType,
                            currentIndex = 0,
                            item = list,
                            itemType = objType,                            
                            collectionTypeCacheItem = collectionTypeCacheItem,
                            writeTypeInfo = writeTypeInfo,
                            parentJob = parentJob,
                            itemName = parentJob == null ? writer.PrepareRootName() :
                                    parentJob is ComplexJob complexParentJob ? complexParentJob.currentFieldName :
                                    writer.PrepareCollectionIndexName((EnumarableJob)parentJob)

                        };
                        jobStack.Push(job);
                        if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef && objType.IsClass) objToJob[list] = job;
                    }
                    else if (obj is IEnumerable enumerable)
                    {
                        EnumarableJob job = new()
                        {
                            collectionType = collectionType,
                            currentIndex = 0,
                            item = enumerable,
                            itemType = objType,
                            enumerator = enumerable.GetEnumerator(),
                            collectionTypeCacheItem = collectionTypeCacheItem,
                            writeTypeInfo = writeTypeInfo,
                            parentJob = parentJob,
                            itemName = parentJob == null ? writer.PrepareRootName() :
                                    parentJob is ComplexJob complexParentJob ? complexParentJob.currentFieldName :
                                    writer.PrepareCollectionIndexName((EnumarableJob)parentJob)

                        };
                        jobStack.Push(job);
                        if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef && objType.IsClass) objToJob[enumerable] = job;
                    }                    
                };
            }
            bool collectionHandled = typeCacheItem.itemHandler != null;
            return collectionHandled;
        }

        private TypeCacheItem CreateTypeCacheItem_Enum(Type objType, TypeCacheItem typeCacheItem, byte[] preparedTypeInfo)
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

        private TypeCacheItem CreateTypeCacheItem_Primitive(Type objType, TypeCacheItem typeCacheItem, byte[] preparedTypeInfo)
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

        private TypeCacheItem CreateTypeCacheItem_String(TypeCacheItem typeCacheItem, byte[] preparedTypeInfo)
        {
            typeCacheItem.itemHandler = (obj, parentJob, writeTypeInfo) =>
            {
                prepareTypeInfoObjectFromBytes(writeTypeInfo, preparedTypeInfo);
                writer.WriteStringValue((string)obj);
                finishTypeInfoObject(writeTypeInfo);
            };
            return typeCacheItem;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<string> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteStringValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteStringValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<string> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteStringValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteStringValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<int> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteSignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<int> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteSignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<sbyte> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteSignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<sbyte> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteSignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<short> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteSignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<short> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteSignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<long> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteSignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<long> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteSignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteSignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<byte> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteUnsignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<byte> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteUnsignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<uint> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteUnsignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<uint> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteUnsignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<ushort> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteUnsignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<ushort> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteUnsignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<ulong> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteUnsignedIntValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<ulong> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteUnsignedIntValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteUnsignedIntValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<float> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteFloatValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteFloatValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<float> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteFloatValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteFloatValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<double> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteFloatValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteFloatValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<double> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteFloatValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteFloatValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection(ICollection<bool> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WriteBoolValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WriteBoolValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList(IList<bool> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WriteBoolValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WriteBoolValue(items[index++]);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveCollection<T>(ICollection<T> items)
        {
            writer.OpenCollection();

            var enumerator = items.GetEnumerator();
            if (enumerator.MoveNext())
            {
                writer.WritePrimitiveValue(enumerator.Current);
            }
            while (enumerator.MoveNext())
            {
                writer.WriteComma();
                writer.WritePrimitiveValue(enumerator.Current);
            }

            writer.CloseCollection();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializePrimitiveList<T>(IList<T> items)
        {
            writer.OpenCollection();

            int index = 0;
            if (index < items.Count)
            {
                writer.WritePrimitiveValue(items[index++]);
            }
            while (index < items.Count)
            {
                writer.WriteComma();
                writer.WritePrimitiveValue(items[index++]);
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

            if (memberType == typeof(string)) return CreatePrimitiveFieldWriter<string>(objType, memberInfo, extendedFieldNameBytes, writer.WriteStringValue);            
            else if (memberType == typeof(int)) return CreatePrimitiveFieldWriter<int>(objType, memberInfo, extendedFieldNameBytes, writer.WriteSignedIntValue);
            else if (memberType == typeof(uint)) return CreatePrimitiveFieldWriter<uint>(objType, memberInfo, extendedFieldNameBytes, writer.WriteUnsignedIntValue);
            else if (memberType == typeof(byte)) return CreatePrimitiveFieldWriter<byte>(objType, memberInfo, extendedFieldNameBytes, writer.WriteUnsignedIntValue);
            else if (memberType == typeof(sbyte)) return CreatePrimitiveFieldWriter<sbyte>(objType, memberInfo, extendedFieldNameBytes, writer.WriteSignedIntValue);
            else if (memberType == typeof(short)) return CreatePrimitiveFieldWriter<short>(objType, memberInfo, extendedFieldNameBytes, writer.WriteSignedIntValue);
            else if (memberType == typeof(ushort)) return CreatePrimitiveFieldWriter<ushort>(objType, memberInfo, extendedFieldNameBytes, writer.WriteUnsignedIntValue);
            else if (memberType == typeof(long)) return CreatePrimitiveFieldWriter<long>(objType, memberInfo, extendedFieldNameBytes, writer.WriteSignedIntValue);
            else if (memberType == typeof(ulong)) return CreatePrimitiveFieldWriter<ulong>(objType, memberInfo, extendedFieldNameBytes, writer.WriteUnsignedIntValue);
            else if (memberType == typeof(bool)) return CreatePrimitiveFieldWriter<bool>(objType, memberInfo, extendedFieldNameBytes, writer.WriteBoolValue);
            else if (memberType == typeof(char)) return CreatePrimitiveFieldWriter<char>(objType, memberInfo, extendedFieldNameBytes, writer.WriteCharValue);
            else if (memberType == typeof(float)) return CreatePrimitiveFieldWriter<float>(objType, memberInfo, extendedFieldNameBytes, writer.WriteFloatValue);
            else if (memberType == typeof(double)) return CreatePrimitiveFieldWriter<double>(objType, memberInfo, extendedFieldNameBytes, writer.WriteFloatValue);
            else if (memberType == typeof(IntPtr)) return CreatePrimitiveFieldWriter<IntPtr>(objType, memberInfo, extendedFieldNameBytes, writer.WritePrimitiveValue);
            else if (memberType == typeof(UIntPtr)) return CreatePrimitiveFieldWriter<UIntPtr>(objType, memberInfo, extendedFieldNameBytes, writer.WritePrimitiveValue);            
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

        private FieldWriter CreatePrimitiveFieldWriter<T>(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes, Action<T> write)
        {
            var parameter = Expression.Parameter(typeof(object));
            var castedParameter = Expression.Convert(parameter, objType);
            var fieldAccess = memberInfo is FieldInfo field ? Expression.Field(castedParameter, field) : memberInfo is PropertyInfo property ? Expression.Property(castedParameter, property) : default;
            var lambda = Expression.Lambda<Func<object, T>>(fieldAccess, parameter);
            var getValue = lambda.Compile();

            return (parentJob) =>
            {
                writer.WritePreparedByteString(fieldNameBytes);
                var value = getValue(parentJob.item);
                write(value);
            };
        }

        private FieldWriter CreateEnumFieldWriter(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            Func<object, object> getValue = memberInfo is FieldInfo fieldInfo ? fieldInfo.GetValue : memberInfo is PropertyInfo propertyInfo ? propertyInfo.GetValue : default;
            Action<object> write = settings.enumAsString ? enumValue => writer.WritePreparedByteString(GetEnumText(enumValue, objType)) :
                                                           enumValue => writer.WriteSignedIntValue((int)enumValue);

            return (parentJob) =>
            {
                var value = getValue(parentJob.item);
                writer.WritePreparedByteString(fieldNameBytes);
                write(value);
            };
        }

        private FieldWriter CreateCollectionFieldWriter(Type objType, MemberInfo memberInfo, byte[] extendedFieldNameBytes, byte[] fieldNameBytes)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;
            Type collectionType = memberInfo is FieldInfo field2 ? field2.FieldType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  memberInfo is PropertyInfo property2 ? property2.PropertyType.GetFirstTypeParamOfGenericInterface(typeof(IEnumerable<>)) :
                                  default;
            collectionType = collectionType ?? typeof(object);

            if (collectionType == typeof(string)) return CreatePrimitiveCollectionFieldWriter<string>(objType, memberInfo, extendedFieldNameBytes);
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

                if (items == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                Type objType = items.GetType();
                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && memberType != objType);
                writer.WritePreparedByteString(extendedFieldNameBytes);

                if (items is IList list)
                {
                    ListJob job = new()
                    {
                        collectionType = collectionType,
                        currentIndex = 0,
                        item = list,
                        itemType = objType,
                        collectionTypeCacheItem = collectionTypeCacheItem,
                        writeTypeInfo = writeTypeInfo,
                        parentJob = parentJob,
                        itemName = fieldNameBytes
                    };
                    jobStack.Push(job);
                    if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef && objType.IsClass) objToJob[items] = job;
                }
                else
                {                                                            
                    EnumarableJob job = new()
                    {
                        collectionType = collectionType,
                        currentIndex = 0,
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
                }
            };
        }

        private FieldWriter CreatePrimitiveCollectionFieldWriter<T>(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {            
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;
            if (memberType.IsAssignableTo(typeof(IList<T>))) return CreatePrimitiveListFieldWriter<T>(objType, memberInfo, fieldNameBytes);

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
            else if (typeof(T).IsAssignableTo(typeof(string))) writeValue = value => { if (value is string v) writer.WriteStringValue(v); };

            return (parentJob) =>
            {
                IEnumerable<T> items = getValue(parentJob.item);
                Type objType = items.GetType();
                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && memberType != objType);
                writer.WritePreparedByteString(fieldNameBytes);
                prepareTypeInfoObjectFromType(writeTypeInfo, objType);
                writer.OpenCollection();

                var enumerator = items.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    writeValue(enumerator.Current);
                }

                while (enumerator.MoveNext())
                {
                    writer.WriteComma();
                    writeValue(enumerator.Current);
                }

                writer.CloseCollection();
                finishTypeInfoObject(writeTypeInfo);
            };
        }

        private FieldWriter CreatePrimitiveListFieldWriter<T>(Type objType, MemberInfo memberInfo, byte[] fieldNameBytes)
        {
            Type memberType = memberInfo is FieldInfo field ? field.FieldType : memberInfo is PropertyInfo property ? property.PropertyType : default;

            var parameter = Expression.Parameter(typeof(object));
            Type declaringType = memberInfo is FieldInfo field2 ? field2.DeclaringType : memberInfo is PropertyInfo property2 ? property2.DeclaringType : default;
            var castedParameter = Expression.Convert(parameter, declaringType);
            var fieldAccess = memberInfo is FieldInfo field3 ? Expression.Field(castedParameter, field3) : memberInfo is PropertyInfo property3 ? Expression.Property(castedParameter, property3) : default;
            var castFieldAccess = Expression.Convert(fieldAccess, typeof(IList<T>));
            var lambda = Expression.Lambda<Func<object, IList<T>>>(castFieldAccess, parameter);
            var getValue = lambda.Compile();

            Action<T> writeValue = writer.WritePrimitiveValue;
            if (typeof(T).IsAssignableTo(typeof(int))) writeValue = value => { if (value is int v) writer.WriteSignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(uint))) writeValue = value => { if (value is uint v) writer.WriteUnsignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(long))) writeValue = value => { if (value is long v) writer.WriteSignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(ulong))) writeValue = value => { if (value is ulong v) writer.WriteUnsignedIntValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(float))) writeValue = value => { if (value is float v) writer.WriteFloatValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(double))) writeValue = value => { if (value is double v) writer.WriteFloatValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(bool))) writeValue = value => { if (value is bool v) writer.WriteBoolValue(v); };
            else if (typeof(T).IsAssignableTo(typeof(string))) writeValue = value => { if (value is string v) writer.WriteStringValue(v); };

            return (parentJob) =>
            {
                IList<T> items = getValue(parentJob.item);
                Type objType = items.GetType();
                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo || (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && memberType != objType);
                writer.WritePreparedByteString(fieldNameBytes);
                prepareTypeInfoObjectFromType(writeTypeInfo, objType);
                writer.OpenCollection();

                int index = 0;
                if (index < items.Count)
                {
                    writeValue(items[index++]);
                }

                while (index < items.Count)
                {
                    writer.WriteComma();
                    writeValue(items[index++]);
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
