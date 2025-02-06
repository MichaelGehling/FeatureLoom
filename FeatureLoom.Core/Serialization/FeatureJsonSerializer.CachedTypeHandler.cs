using FeatureLoom.Helpers;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonSerializer
    {

        public interface ICachedTypeHandler
        {
            void SetItemHandler<T>(ItemHandler<T> itemHandler, JsonDataTypeCategory category);
        }

        public sealed class CachedTypeHandler : ICachedTypeHandler
        {
            private FeatureJsonSerializer serializer;
            private JsonUTF8StreamWriter writer;
            private Delegate itemHandler;
            private Action<object, Type, ByteSegment> objectItemHandler;
            private Type handlerType;
            private bool isPrimitive;
            private bool noRefTypes;
            public byte[] preparedTypeInfo;

            public CachedTypeHandler(FeatureJsonSerializer serializer)
            {
                this.serializer = serializer;
                this.writer = serializer.writer;                
            }

            public bool IsPrimitive => isPrimitive;
            public bool NoRefTypes => noRefTypes;

            public Type HandlerType => handlerType;

            public void SetItemHandler<T>(ItemHandler<T> itemHandler, JsonDataTypeCategory category)
            {
                switch (category)
                {
                    case JsonDataTypeCategory.Primitive: SetItemHandler_Primitive(itemHandler); break;
                    case JsonDataTypeCategory.Array: SetItemHandler_Array(itemHandler, false); break;
                    case JsonDataTypeCategory.Array_WithoutRefChildren: SetItemHandler_Array(itemHandler, true); break;
                    case JsonDataTypeCategory.Object: SetItemHandler_Object(itemHandler, false); break;
                    case JsonDataTypeCategory.Object_WithoutRefChildren: SetItemHandler_Object(itemHandler, true); break;
                }
            }

            public void SetItemHandler_Primitive<T>(ItemHandler<T> itemHandler)
            {
                this.handlerType = typeof(T);
                this.isPrimitive = true;
                this.noRefTypes = true;
                Action<T, Type, ByteSegment> temp;
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
                    temp = (item, _, _) =>
                    {
                        itemHandler.Invoke(item);
                    };
                }
                this.itemHandler = temp;
                this.objectItemHandler = (item, callType, itemName) => temp.Invoke((T)item, callType, itemName);
            }

            public void SetItemHandler_Array<T>(ItemHandler<T> itemHandler, bool noRefChildren)
            {
                this.handlerType = typeof(T);
                this.isPrimitive = false;
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<T, Type, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                    {
                        if (!this.handlerType.IsValueType)
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item, callType))
                                {
                                    writer.OpenArray();
                                    itemHandler.Invoke(item);
                                    writer.CloseArray();
                                }
                                serializer.UseParentItemInfo();
                            };
                        }
                        else
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                writer.OpenArray();
                                itemHandler.Invoke(item);
                                writer.CloseArray();
                                serializer.UseParentItemInfo();
                            };
                        }
                    }
                    else
                    {
                        if (!this.handlerType.IsValueType)
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item, callType))
                                {
                                    Type itemType = item.GetType();
                                    bool writeTypeInfo = serializer.TypeInfoRequired(itemType, callType);
                                    if (writeTypeInfo) serializer.StartTypeInfoObject(preparedTypeInfo);
                                    writer.OpenArray();
                                    itemHandler.Invoke(item);
                                    writer.CloseArray();
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
                                writer.OpenArray();
                                itemHandler.Invoke(item);
                                writer.CloseArray();
                                if (writeTypeInfo) serializer.FinishTypeInfoObject();
                                serializer.UseParentItemInfo();
                            };
                        }
                    }
                }
                else
                {
                    if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                    {
                        temp = (item, callType, itemName) =>
                        {
                            writer.OpenArray();
                            itemHandler.Invoke(item);
                            writer.CloseArray();
                        };
                    }
                    else
                    {
                        temp = (item, callType, itemName) =>
                        {
                            Type itemType = item.GetType();
                            bool writeTypeInfo = serializer.TypeInfoRequired(itemType, callType);
                            if (writeTypeInfo) serializer.StartTypeInfoObject(preparedTypeInfo);
                            writer.OpenArray();
                            itemHandler.Invoke(item);
                            writer.CloseArray();
                            if (writeTypeInfo) serializer.FinishTypeInfoObject();
                        };
                    }
                }
                this.itemHandler = temp;
                this.objectItemHandler = (item, callType, itemName) => temp.Invoke((T)item, callType, itemName);
            }

            public void SetItemHandler_Object<T>(ItemHandler<T> itemHandler, bool noRefChildren)
            {
                this.handlerType = typeof(T);
                this.isPrimitive = false;                
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<T, Type, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (!this.handlerType.IsValueType)
                    {
                        if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item, callType))
                                {
                                    writer.OpenObject();
                                    itemHandler.Invoke(item);                                    
                                    writer.CloseObject();
                                }
                                serializer.UseParentItemInfo();
                            };
                        }
                        else
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item, callType))
                                {
                                    Type itemType = item.GetType();
                                    writer.OpenObject();
                                    int tempBufferCount1 = -1;
                                    int tempBufferCount2 = -1;
                                    if (serializer.TypeInfoRequired(itemType, callType))
                                    {
                                        writer.WriteToBuffer(preparedTypeInfo);
                                        tempBufferCount1 = writer.BufferCount;
                                        writer.WriteComma();
                                        tempBufferCount2 = writer.BufferCount;
                                    }
                                    itemHandler.Invoke(item);
                                    if (writer.BufferCount == tempBufferCount2) writer.BufferCount = tempBufferCount1;
                                    writer.CloseObject();
                                }
                                serializer.UseParentItemInfo();
                            };
                        }
                    }
                    else
                    {
                        if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                writer.OpenObject();
                                itemHandler.Invoke(item);
                                writer.CloseObject();
                                serializer.UseParentItemInfo();
                            };
                        }
                        else
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                Type itemType = item.GetType();
                                writer.OpenObject();
                                int tempBufferCount1 = -1;
                                int tempBufferCount2 = -1;
                                if (serializer.TypeInfoRequired(itemType, callType))
                                {
                                    writer.WriteToBuffer(preparedTypeInfo);
                                    tempBufferCount1 = writer.BufferCount;
                                    writer.WriteComma();
                                    tempBufferCount2 = writer.BufferCount;
                                }
                                itemHandler.Invoke(item);
                                if (writer.BufferCount == tempBufferCount2) writer.BufferCount = tempBufferCount1;
                                writer.CloseObject();
                                serializer.UseParentItemInfo();
                            };
                        }
                    }
                }
                else
                {
                    if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                    {
                        temp = (item, callType, itemName) =>
                        {
                            writer.OpenObject();
                            itemHandler.Invoke(item);
                            writer.CloseObject();
                        };
                    }
                    else
                    {
                        temp = (item, callType, itemName) =>
                        {
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            int tempBufferCount1 = -1;
                            int tempBufferCount2 = -1;
                            if (serializer.TypeInfoRequired(itemType, callType))
                            {
                                writer.WriteToBuffer(preparedTypeInfo);
                                tempBufferCount1 = writer.BufferCount;
                                writer.WriteComma();
                                tempBufferCount2 = writer.BufferCount;
                            }
                            itemHandler.Invoke(item);
                            if (writer.BufferCount == tempBufferCount2) writer.BufferCount = tempBufferCount1;
                            writer.CloseObject();
                        };
                    } 
                }

                this.itemHandler = temp;
                this.objectItemHandler = (item, _, fieldName) => temp.Invoke((T)item, _, fieldName);
            }


            public void SetItemHandler_Object<T>(Action<T>[] fieldHandlers, bool noRefChildren)
            {
                this.handlerType = typeof(T);
                this.isPrimitive = false;
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<T, Type, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (!this.handlerType.IsValueType)
                    {
                        if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item, callType))
                                {
                                    writer.OpenObject();
                                    if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                                    for (int i = 1; i< fieldHandlers.Length; i++)
                                    {
                                        writer.WriteComma();
                                        fieldHandlers[i].Invoke(item);
                                    }
                                    writer.CloseObject();
                                }
                                serializer.UseParentItemInfo();
                            };
                        }
                        else
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item, callType))
                                {
                                    Type itemType = item.GetType();
                                    writer.OpenObject();
                                    if (serializer.TypeInfoRequired(itemType, callType))
                                    {
                                        writer.WriteToBuffer(preparedTypeInfo);
                                        if (fieldHandlers.Length >= 1) writer.WriteComma();
                                    }
                                    if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                                    for (int i = 1; i < fieldHandlers.Length; i++)
                                    {
                                        writer.WriteComma();
                                        fieldHandlers[i].Invoke(item);
                                    }
                                    writer.CloseObject();
                                }
                                serializer.UseParentItemInfo();
                            };
                        }
                    }
                    else
                    {
                        if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                writer.OpenObject();
                                if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                                for (int i = 1; i < fieldHandlers.Length; i++)
                                {
                                    writer.WriteComma();
                                    fieldHandlers[i].Invoke(item);
                                }
                                writer.CloseObject();
                                serializer.UseParentItemInfo();
                            };
                        }
                        else
                        {
                            temp = (item, callType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                Type itemType = item.GetType();
                                writer.OpenObject();
                                if (serializer.TypeInfoRequired(itemType, callType))
                                {
                                    writer.WriteToBuffer(preparedTypeInfo);
                                    if (fieldHandlers.Length >= 1) writer.WriteComma();
                                }
                                if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                                for (int i = 1; i < fieldHandlers.Length; i++)
                                {
                                    writer.WriteComma();
                                    fieldHandlers[i].Invoke(item);
                                }
                                writer.CloseObject();
                                serializer.UseParentItemInfo();
                            };
                        }
                    }
                }
                else
                {
                    if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                    {
                        temp = (item, callType, itemName) =>
                        {
                            writer.OpenObject();
                            if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                            for (int i = 1; i < fieldHandlers.Length; i++)
                            {
                                writer.WriteComma();
                                fieldHandlers[i].Invoke(item);
                            }
                            writer.CloseObject();
                        };
                    }
                    else
                    {
                        temp = (item, callType, itemName) =>
                        {
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            if (serializer.TypeInfoRequired(itemType, callType))
                            {
                                writer.WriteToBuffer(preparedTypeInfo);
                                if (fieldHandlers.Length >= 1) writer.WriteComma();
                            }
                            if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                            for (int i = 1; i < fieldHandlers.Length; i++)
                            {
                                writer.WriteComma();
                                fieldHandlers[i].Invoke(item);
                            }
                            writer.CloseObject();
                        };
                    }
                }

                this.itemHandler = temp;
                this.objectItemHandler = (item, _, fieldName) => temp.Invoke((T)item, _, fieldName);
            }
            public void SetItemHandler_Object_ForNullableStruct<T>(Action<T>[] fieldHandlers, bool noRefChildren) where T : struct
            {
                this.handlerType = typeof(Nullable<T>);
                this.isPrimitive = false;
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<Nullable<T>, Type, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                    {
                        temp = (nullableItem, callType, itemName) =>
                        {
                            if (!nullableItem.HasValue)
                            {
                                writer.WriteNullValue();
                                return;
                            }
                            T item = nullableItem.Value;
                            serializer.CreateItemInfoForStruct(itemName);
                            writer.OpenObject();
                            if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                            for (int i = 1; i < fieldHandlers.Length; i++)
                            {
                                writer.WriteComma();
                                fieldHandlers[i].Invoke(item);
                            }
                            writer.CloseObject();
                            serializer.UseParentItemInfo();
                        };
                    }
                    else
                    {
                        temp = (nullableItem, callType, itemName) =>
                        {
                            if (!nullableItem.HasValue)
                            {
                                writer.WriteNullValue();
                                return;
                            }
                            T item = nullableItem.Value;
                            serializer.CreateItemInfoForStruct(itemName);
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            if (serializer.TypeInfoRequired(itemType, callType))
                            {
                                writer.WriteToBuffer(preparedTypeInfo);
                                if (fieldHandlers.Length >= 1) writer.WriteComma();
                            }
                            if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                            for (int i = 1; i < fieldHandlers.Length; i++)
                            {
                                writer.WriteComma();
                                fieldHandlers[i].Invoke(item);
                            }
                            writer.CloseObject();
                            serializer.UseParentItemInfo();
                        };
                    }
                    
                }
                else
                {
                    if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                    {
                        temp = (nullableItem, callType, itemName) =>
                        {
                            if (!nullableItem.HasValue)
                            {
                                writer.WriteNullValue();
                                return;
                            }
                            T item = nullableItem.Value;
                            writer.OpenObject();
                            if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                            for (int i = 1; i < fieldHandlers.Length; i++)
                            {
                                writer.WriteComma();
                                fieldHandlers[i].Invoke(item);
                            }
                            writer.CloseObject();
                        };
                    }
                    else
                    {
                        temp = (nullableItem, callType, itemName) =>
                        {
                            if (!nullableItem.HasValue)
                            {
                                writer.WriteNullValue();
                                return;
                            }
                            T item = nullableItem.Value;
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            if (serializer.TypeInfoRequired(itemType, callType))
                            {
                                writer.WriteToBuffer(preparedTypeInfo);
                                if (fieldHandlers.Length >= 1) writer.WriteComma();
                            }
                            if (fieldHandlers.Length >= 1) fieldHandlers[0].Invoke(item);
                            for (int i = 1; i < fieldHandlers.Length; i++)
                            {
                                writer.WriteComma();
                                fieldHandlers[i].Invoke(item);
                            }
                            writer.CloseObject();
                        };
                    }
                }

                this.itemHandler = temp;
                this.objectItemHandler = (item, _, fieldName) => temp.Invoke((T)item, _, fieldName);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void HandleItem<T>(T item, ByteSegment fieldName)
            {
                Type callType = typeof(T);
                if (callType == handlerType)
                {
                    Action<T, Type, ByteSegment> typedItemHandler = (Action<T, Type, ByteSegment >)itemHandler;
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

        public void SetWriterMethod<T>(Func<T, ByteSegment> writerDelegate) => this.writerDelegateWithCopy = writerDelegate;
        public void SetWriterMethod<T>(Action<T> writerDelegate) => this.writerDelegate = writerDelegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteKeyAsStringWithCopy<T>(T item)
        {
            if (skipCopy)
            {
                var write = (Action<T>)writerDelegate;
                write(item);
                return default;
            }
            else
            {
                var write = (Func<T, ByteSegment>)writerDelegateWithCopy;
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
