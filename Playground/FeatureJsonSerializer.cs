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
        StackJobRecycler<EnumerableStackJob> enumerableStackJobRecycler;
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
            enumerableStackJobRecycler = new(this);
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
            enumerableStackJobRecycler.RecyclePostponedJobs();
        }

        CachedTypeHandler lastTypeHandler = null;
        Type lastTypeHandlerType = null;

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

                    if (lastTypeHandlerType == itemType)
                    {
                        lastTypeHandler.HandleItem(item, expectedType, null);
                    }
                    else
                    {
                        var typeHandler = GetCachedTypeHandler(itemType);
                        typeHandler.HandleItem(item, expectedType, null);

                        lastTypeHandler = typeHandler;
                        lastTypeHandlerType = typeHandler.HandlerType;
                    }

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

                    if (lastTypeHandlerType == itemType)
                    {
                        lastTypeHandler.HandleItem(item, expectedType, null);
                    }
                    else
                    {
                        var typeHandler = GetCachedTypeHandler(itemType);
                        typeHandler.HandleItem(item, expectedType, null);

                        lastTypeHandler = typeHandler;
                        lastTypeHandlerType = typeHandler.HandlerType;
                    }

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

            typeHandler.preparedTypeInfo = writer.PrepareTypeInfo(itemType.GetSimplifiedTypeName());

            if (itemType == typeof(int)) CreatePrimitiveItemHandler<int>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(uint)) CreatePrimitiveItemHandler<uint>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(long)) CreatePrimitiveItemHandler<long>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(ulong)) CreatePrimitiveItemHandler<ulong>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(short)) CreatePrimitiveItemHandler<short>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(ushort)) CreatePrimitiveItemHandler<ushort>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(sbyte)) CreatePrimitiveItemHandler<sbyte>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(byte)) CreatePrimitiveItemHandler<byte>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(string)) CreatePrimitiveItemHandler<string>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(float)) CreatePrimitiveItemHandler<float>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(double)) CreatePrimitiveItemHandler<double>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(char)) CreatePrimitiveItemHandler<char>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(IntPtr)) CreatePrimitiveItemHandler<IntPtr>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(UIntPtr)) CreatePrimitiveItemHandler<UIntPtr>(typeHandler, writer.WritePrimitiveValue);
            else if (itemType == typeof(Guid)) CreatePrimitiveItemHandler<Guid>(typeHandler, writer.WritePrimitiveValue); //Make specialized
            else if (itemType == typeof(DateTime)) CreatePrimitiveItemHandler<DateTime>(typeHandler, writer.WritePrimitiveValue); //Make specialized
            else if (itemType.IsEnum) CreateAndSetItemHandlerViaReflection(typeHandler, itemType, nameof(CreateEnumItemHandler), true);
            else if (TryCreateDictionaryItemHandler(typeHandler, itemType)) /* do nothing */;
            else if (TryCreateListItemHandler(typeHandler, itemType)) /* do nothing */;
            else if (TryCreateEnumerableItemHandler(typeHandler, itemType)) /* do nothing */;

            //else throw new Exception($"No handler available for {itemType}");
            else typeHandler.SetItemHandler<object>((_, _, _) => writer.WritePrimitiveValue($"Unsupported Type {itemType.GetSimplifiedTypeName()}"), false);
            
            return typeHandler;

            void CreateAndSetItemHandlerViaReflection(CachedTypeHandler typeHandler, Type itemType, string getItemHandlerMethodName, bool isPrimitive)
            {
                MethodInfo method = typeof(FeatureJsonSerializer).GetMethod(getItemHandlerMethodName, BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo generic = method.MakeGenericMethod(itemType);
                generic.Invoke(this, new object[] { typeHandler });
            }
        }

        private void CreatePrimitiveItemHandler<T>(CachedTypeHandler typeHandler, Action<T> write)
        {
            if (settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
            {
                typeHandler.SetItemHandler<T>((item, _, _) => write(item), true);
            }
            else if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
            {
                typeHandler.SetItemHandler<T>((item, _, _) =>
                {
                    StartTypeInfoObject(typeHandler.preparedTypeInfo);
                    write(item);
                    FinishTypeInfoObject();
                }, true);
            }
            else
            {
                typeHandler.SetItemHandler<T>((item, expectedType, _) =>
                {
                    if (expectedType == item.GetType())
                    {                        
                        write(item);                        
                    }
                    else
                    {
                        StartTypeInfoObject(typeHandler.preparedTypeInfo);
                        write(item);
                        FinishTypeInfoObject();
                    }
                }, true);
            }
        }

        private void CreateEnumItemHandler<T>(CachedTypeHandler typeHandler) where T : struct, Enum
        {
            if (settings.enumAsString)
            {                
                CreatePrimitiveItemHandler<T>(typeHandler, item => writer.WritePrimitiveValue(item.ToName()));
            }
            else
            {
                CreatePrimitiveItemHandler<T>(typeHandler, item => writer.WritePrimitiveValue(item.ToInt()));
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void StartTypeInfoObject(byte[] preparedTypeInfo)
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
                RefJob newRefJob = refJobRecycler.GetJob(parentJob, CreateItemName(parentJob), obj);
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
