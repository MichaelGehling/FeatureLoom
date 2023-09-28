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
using static Playground.LoopJsonSerializer;
using System.Data;
using System.Reflection.Metadata;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using FeatureLoom.Helpers;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        FeatureLock serializerLock = new FeatureLock();
        Stack<StackJob> jobStack = new Stack<StackJob>();
        Dictionary<object, RefJob> objToJob = new();
        MemoryStream memoryStream = new MemoryStream();
        JsonUTF8StreamWriter writer = new JsonUTF8StreamWriter();
        Settings settings;
        Dictionary<Type, CachedTypeHandler> typeHandlerCache = new();
        Dictionary<Type, CachedStringValueWriter> stringValueWriterCache = new();

        delegate void ItemHandler<T>(T item, Type expectedType, StackJob parentJob);

        StackJobRecycler<DictionaryStackJob> dictionaryStackJobRecycler;
        StackJobRecycler<ListStackJob> listStackJobRecycler;
        StackJobRecycler<RefJob> refJobRecycler;

        public FeatureJsonSerializer(Settings settings = null)
        {
            this.settings = settings ?? new();

            InitStackJobRecyclers();

            /*
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
            */
        }

        void InitStackJobRecyclers()
        {
            dictionaryStackJobRecycler = new(this);
            listStackJobRecycler = new(this);
            refJobRecycler = new(this);
        }

        void FinishSerialization()
        {
            memoryStream.Position = 0;
            writer.stream = null;
            if (objToJob.Count > 0) objToJob.Clear();
            if (jobStack.Count > 0) jobStack.Clear();

            dictionaryStackJobRecycler.RecyclePostponedJobs();    
            refJobRecycler.RecyclePostponedJobs();
            listStackJobRecycler.RecyclePostponedJobs();
        }

        public string Serialize<T>(T item)
        {
            using (serializerLock.Lock())
            {
                try
                {
                    writer.stream = memoryStream;

                    if (item == null)
                    {
                        return "null";
                    }
                    Type expectedType = typeof(T);
                    Type itemType = item.GetType();
                    var typeHandler = GetCachedTypeHandler(itemType);
                    typeHandler.HandleItem(item, expectedType, null);

                    LoopJobStack();

                    return Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                }
                finally
                {
                    FinishSerialization();
                }
            }
        }

        public void Serialize<T>(Stream stream, T item)
        {
            using (serializerLock.Lock())
            {
                try
                {
                    writer.stream = stream;

                    if (item == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }
                    Type expectedType = typeof(T);
                    Type itemType = item.GetType();
                    var typeHandler = GetCachedTypeHandler(itemType);
                    typeHandler.HandleItem(item, expectedType, null);

                    LoopJobStack();
                }
                finally
                {
                    FinishSerialization();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddJobToStack(StackJob job)
        {
            jobStack.Push(job);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void LoopJobStack()
        {
            while (jobStack.TryPeek(out StackJob job))
            {
                bool finished = job.Process();
                if (finished)
                {
                    jobStack.Pop();
                    job.Recycle();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetCachedStringValueWriter(Type itemType, out CachedStringValueWriter stringValueWriter) => stringValueWriterCache.TryGetValue(itemType, out stringValueWriter) || TryCreateStringValueWriter(itemType, out stringValueWriter);

        private bool TryCreateStringValueWriter(Type itemType, out CachedStringValueWriter stringValueWriter)
        {
            stringValueWriter = new();

            if (itemType == typeof(string)) stringValueWriter.SetWriterMethod<string>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(bool)) stringValueWriter.SetWriterMethod<bool>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(char)) stringValueWriter.SetWriterMethod<char>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(sbyte)) stringValueWriter.SetWriterMethod<sbyte>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(short)) stringValueWriter.SetWriterMethod<short>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(int)) stringValueWriter.SetWriterMethod<int>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(long)) stringValueWriter.SetWriterMethod<long>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(byte)) stringValueWriter.SetWriterMethod<byte>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(ushort)) stringValueWriter.SetWriterMethod<ushort>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(uint)) stringValueWriter.SetWriterMethod<uint>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(ulong)) stringValueWriter.SetWriterMethod<ulong>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(Guid)) stringValueWriter.SetWriterMethod<Guid>(writer.WritePrimitiveValueAsString);
            else if (itemType == typeof(DateTime)) stringValueWriter.SetWriterMethod<DateTime>(writer.WritePrimitiveValueAsString);

            return stringValueWriter.HasMethod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CachedTypeHandler GetCachedTypeHandler(Type itemType) => typeHandlerCache.TryGetValue(itemType, out var typeCacheItem) ? typeCacheItem : CreateCachedTypeHandler(itemType);

        private CachedTypeHandler CreateCachedTypeHandler(Type itemType)
        {
            CachedTypeHandler typeHandler = new CachedTypeHandler();
            typeHandlerCache[itemType] = typeHandler;

            byte[] preparedTypeInfo = writer.PrepareTypeInfo(itemType.GetSimplifiedTypeName());

            if (itemType == typeof(int)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<int>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(uint)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<uint>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(long)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<long>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(ulong)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<ulong>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(short)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<short>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(ushort)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<ushort>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(sbyte)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<sbyte>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(byte)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<byte>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(string)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<string>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(float)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<float>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(double)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<double>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(char)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<char>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(IntPtr)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<IntPtr>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(UIntPtr)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<UIntPtr>(writer.WritePrimitiveValue, preparedTypeInfo), true);
            else if (itemType == typeof(Guid)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<Guid>(writer.WritePrimitiveValue, preparedTypeInfo), true); //Make specialized
            else if (itemType == typeof(DateTime)) typeHandler.SetItemHandler(GetPrimitiveItemHandler<DateTime>(writer.WritePrimitiveValue, preparedTypeInfo), true); //Make specialized
            else if (itemType.IsEnum) CreateAndSetItemHandlerViaReflection(typeHandler, itemType, nameof(GetEnumItemHandler), preparedTypeInfo, true);
            else if (TryCreateDictionaryItemHandler(typeHandler, itemType, preparedTypeInfo)) /* do nothing */;
            else if (TryCreateListItemHandler(typeHandler, itemType, preparedTypeInfo)) /* do nothing */;
            //else if (TryCreateEnumerableItemHandler(typeHandler, itemType, preparedTypeInfo)) /* do nothing */;

            //else throw new Exception($"No handler available for {itemType}");
            else typeHandler.SetItemHandler<object>((_, _, _) => writer.WritePrimitiveValue($"Unsupported Type {itemType.GetSimplifiedTypeName()}"), false);
            
            return typeHandler;

            void CreateAndSetItemHandlerViaReflection(CachedTypeHandler typeHandler, Type itemType, string getItemHandlerMethodName, byte[] preparedTypeInfo, bool isPrimitive)
            {
                MethodInfo method = typeof(FeatureJsonSerializer).GetMethod(getItemHandlerMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(itemType);
                var itemHandler = generic.Invoke(this, new object[] { preparedTypeInfo });

                MethodInfo genericSetMethod = CachedTypeHandler.setItemHandlerMethodInfo.MakeGenericMethod(itemType);
                genericSetMethod.Invoke(typeHandler, new object[] { itemHandler, isPrimitive });
            }
        }

        private ItemHandler<T> GetPrimitiveItemHandler<T>(Action<T> write, byte[] preparedTypeInfo)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
            {
                return (item, _, _) => write(item);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                return (item, _, _) =>
                {
                    PrepareTypeInfoObject(preparedTypeInfo);
                    write(item);
                    FinishTypeInfoObject();
                };
            }
            else
            {
                return (item, expectedType, _) =>
                {
                    if (expectedType == item.GetType())
                    {                        
                        write(item);                        
                    }
                    else
                    {
                        PrepareTypeInfoObject(preparedTypeInfo);
                        write(item);
                        FinishTypeInfoObject();
                    }
                };
            }
        }

        private ItemHandler<T> GetEnumItemHandler<T>(byte[] preparedTypeInfo) where T : struct, Enum
        {
            if (settings.enumAsString)
            {                
                return GetPrimitiveItemHandler<T>(item => writer.WritePrimitiveValue(item.ToName()), preparedTypeInfo);
            }
            else
            {
                return GetPrimitiveItemHandler<T>(item => writer.WritePrimitiveValue(item.ToInt()), preparedTypeInfo);
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void PrepareTypeInfoObject(byte[] preparedTypeInfo)
        {
            writer.OpenObject();
            writer.WritePreparedByteString(preparedTypeInfo);
            writer.WriteComma();
            writer.WriteValueFieldName();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FinishTypeInfoObject()
        {
            writer.CloseObject();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        byte[] CreateItemName(StackJob parentJob)
        {
            if (parentJob == null) return writer.PrepareRootName();
            return parentJob.GetCurrentChildItemName();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryHandleItemAsRef<T>(T item, StackJob parentJob, Type itemType)
        {
            if (settings.referenceCheck == ReferenceCheck.NoRefCheck || item == null || !itemType.IsClass) return false;
            return TryHandleObjAsRef(item, parentJob, itemType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryHandleObjAsRef(object obj, StackJob parentJob, Type itemType)
        {
            if (settings.referenceCheck == ReferenceCheck.AlwaysReplaceByRef)
            {
                RefJob newRefJob = refJobRecycler.GetJob(parentJob, parentJob?.GetCurrentChildItemName() ?? writer.PrepareRootName(), obj);
                newRefJob.Recycle(); // Actual recycling will be postponed based on ReferenceCheck.AlwaysReplaceByRef setting.
                if (!objToJob.TryAdd(obj, newRefJob))
                {
                    writer.WriteRefObject(objToJob[obj]);
                    return true;
                }
            }
            else
            {
                while(parentJob != null)
                {
                    if (parentJob.objItem == obj)
                    {
                        if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByRef) writer.WriteRefObject(parentJob);
                        else if (settings.referenceCheck == ReferenceCheck.OnLoopReplaceByNull) writer.WriteNullValue();
                        else if (settings.referenceCheck == ReferenceCheck.OnLoopThrowException) throw new Exception("Circular referencing detected!");
                        return true;
                    }
                    parentJob = parentJob.parentJob;
                }
            }

            return false;
        }

    }
}
