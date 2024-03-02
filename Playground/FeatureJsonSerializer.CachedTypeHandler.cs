using FeatureLoom.Helpers;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {

        sealed class CachedTypeHandler
        {
            public readonly static MethodInfo setItemHandlerMethodInfo = typeof(CachedTypeHandler).GetMethod("SetItemHandler");
            private FeatureJsonSerializer serializer;
            private Delegate itemHandler;
            private Action<object, Type, ArraySegment<byte>> objectItemHandler;
            private Type handlerType;
            private bool isPrimitive;
            private bool noRefTypes;
            public byte[] preparedTypeInfo;

            public CachedTypeHandler(FeatureJsonSerializer serializer)
            {
                this.serializer = serializer;
            }

            public bool IsPrimitive => isPrimitive;
            public bool NoRefTypes => noRefTypes;

            public Type HandlerType => handlerType;

            public void SetItemHandler_Primitive<T>(PrimitiveItemHandler<T> itemHandler)
            {
                this.handlerType = typeof(T);
                this.isPrimitive = true;
                this.noRefTypes = true;
                Action<T, Type, ArraySegment<byte>> temp;
                if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo)
                {
                    temp = (item, _, _) =>
                    {
                        serializer.StartTypeInfoObject(preparedTypeInfo);
                        itemHandler.Invoke(item);
                        serializer.FinishTypeInfoObject();
                    };
                }
                else if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo)
                {
                    temp = (item, callType, _) =>
                    {
                        if (handlerType == callType)
                        {
                            itemHandler.Invoke(item);
                        }
                        else
                        {
                            serializer.StartTypeInfoObject(preparedTypeInfo);
                            itemHandler.Invoke(item);
                            serializer.FinishTypeInfoObject();
                        }
                    };
                }
                else
                {
                    temp = (item, expectedType, itemInfo) =>
                    {
                        itemHandler.Invoke(item);
                    };
                }
                this.itemHandler = temp;
                this.objectItemHandler = (item, expectedType, baseJob) => temp.Invoke((T)item, expectedType, baseJob);
            }

            public void SetItemHandler_Array<T>(ItemHandler<T> itemHandler, bool noRefChildren)
            {
                this.handlerType = typeof(T);
                this.isPrimitive = false;
                this.noRefTypes = noRefChildren && !this.handlerType.IsClass;
                Action<T, Type, ArraySegment<byte>> temp;
                if (this.handlerType.IsClass)
                {                    
                    temp = (item, callType, itemName) =>
                    {
                        serializer.CreateItemInfoForClass(item, itemName);
                        if (!serializer.TryHandleItemAsRef(item, serializer.currentItemInfo, callType))
                        {
                            Type itemType = item.GetType();
                            bool writeTypeInfo = serializer.TypeInfoRequired(itemType, callType);
                            if (writeTypeInfo) serializer.StartTypeInfoObject(preparedTypeInfo);
                            serializer.writer.OpenCollection();
                            itemHandler.Invoke(item);
                            serializer.writer.CloseCollection();
                            if (writeTypeInfo) serializer.FinishTypeInfoObject();
                        }
                        serializer.UseParentItemInfo();
                    };
                }
                else
                {
                    temp = (item, callType, itemName) =>
                    {
                        serializer.CreateItemInfoForStruct(itemName);
                        Type itemType = item.GetType();
                        bool writeTypeInfo = serializer.TypeInfoRequired(itemType, callType);
                        if (writeTypeInfo) serializer.StartTypeInfoObject(preparedTypeInfo);
                        serializer.writer.OpenCollection();
                        itemHandler.Invoke(item);
                        serializer.writer.CloseCollection();
                        if (writeTypeInfo) serializer.FinishTypeInfoObject();
                        serializer.UseParentItemInfo();
                    };
                }
                this.itemHandler = temp;
                this.objectItemHandler = (item, callType, baseJob) => temp.Invoke((T)item, callType, baseJob);
            }

            public void SetItemHandler_Object<T>(ItemHandler<T> itemHandler, bool noRefChildren, bool noFields)
            {
                this.handlerType = typeof(T);
                this.isPrimitive = false;                
                this.noRefTypes = noRefChildren && !this.handlerType.IsClass;
                Action<T, Type, ArraySegment<byte>> temp;
                if (this.handlerType.IsClass)
                {
                    if (noFields)
                    {
                        temp = (item, callType, itemName) =>
                        {
                            serializer.CreateItemInfoForClass(item, itemName);
                            if (!serializer.TryHandleItemAsRef(item, serializer.currentItemInfo, callType))
                            {
                                Type itemType = item.GetType();
                                serializer.writer.OpenObject();
                                if (serializer.TypeInfoRequired(itemType, callType)) serializer.writer.WritePreparedByteString(preparedTypeInfo);
                                serializer.writer.CloseObject();
                            }
                            serializer.UseParentItemInfo();
                        };
                    }
                    else
                    {
                        temp = (item, callType, itemName) =>
                        {
                            serializer.CreateItemInfoForClass(item, itemName);
                            if (!serializer.TryHandleItemAsRef(item, serializer.currentItemInfo, callType))
                            {
                                Type itemType = item.GetType();
                                serializer.writer.OpenObject();
                                if (serializer.TypeInfoRequired(itemType, callType))
                                {
                                    serializer.writer.WritePreparedByteString(preparedTypeInfo);
                                    serializer.writer.WriteComma();
                                }
                                itemHandler.Invoke(item);
                                serializer.writer.CloseObject();
                            }
                            serializer.UseParentItemInfo();
                        };
                    }
                }
                else
                {
                    if (noFields)
                    {
                        temp = (item, callType, _) =>
                        {
                            Type itemType = item.GetType();
                            serializer.writer.OpenObject();
                            if (serializer.TypeInfoRequired(itemType, callType)) serializer.writer.WritePreparedByteString(preparedTypeInfo);
                            serializer.writer.CloseObject();
                        };
                    }
                    else
                    {
                        temp = (item, callType, itemName) =>
                        {
                            serializer.CreateItemInfoForStruct(itemName);
                            Type itemType = item.GetType();
                            serializer.writer.OpenObject();
                            if (serializer.TypeInfoRequired(itemType, callType))
                            {
                                serializer.writer.WritePreparedByteString(preparedTypeInfo);
                                serializer.writer.WriteComma();
                            }
                            itemHandler.Invoke(item);
                            serializer.writer.CloseObject();
                            serializer.UseParentItemInfo();
                        };
                    }
                }
                this.itemHandler = temp;
                this.objectItemHandler = (item, _, fieldName) => temp.Invoke((T)item, _, fieldName);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void HandleItem<T>(T item, ArraySegment<byte> fieldName)
            {
                Type callType = typeof(T);
                if (callType == handlerType)
                {
                    Action<T, Type, ArraySegment<byte>> typedItemHandler = (Action<T, Type, ArraySegment<byte> >)itemHandler;
                    typedItemHandler.Invoke(item, callType, fieldName);
                }
                else
                {
                    objectItemHandler(item, callType, fieldName);
                }
            }
        }
    }

    sealed class CachedKeyWriter
    {
        private Delegate writerDelegateWithCopy;
        private Delegate writerDelegate;
        private bool skipCopy;

        public CachedKeyWriter(bool skipCopy)
        {
            this.skipCopy = skipCopy;
        }

        public bool HasMethod => writerDelegate != null;

        public void SetWriterMethod<T>(Func<T, ArraySegment<byte>> writerDelegate) => this.writerDelegateWithCopy = writerDelegate;
        public void SetWriterMethod<T>(Action<T> writerDelegate) => this.writerDelegate = writerDelegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> WriteKeyAsStringWithCopy<T>(T item)
        {
            if (skipCopy)
            {
                var write = (Action<T>)writerDelegate;
                write(item);
                return default;
            }
            else
            {
                var write = (Func<T, ArraySegment<byte>>)writerDelegateWithCopy;
                return write(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteKeyAsString<T>(T item)
        {
            var write = (Action<T>)writerDelegate;
            write(item);
        }
    }
}
