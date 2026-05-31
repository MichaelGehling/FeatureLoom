using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization
{
    public sealed partial class JsonSerializer
    {

        public interface ICachedTypeHandler
        {
            void SetItemHandler<T>(ItemHandler<T> itemHandler, JsonDataTypeCategory category, Type handlerType);
        }

        public sealed class CachedTypeWriter : ICachedTypeHandler
        {
            private JsonSerializer serializer;
            private JsonUTF8StreamWriter writer;
            private Delegate itemWriter; // (T item, bool deviatingType, ByteSegment itemName)
            private Action<object, bool, ByteSegment> objectItemWriter;
            private Type handlerType;
            private bool noRefTypes;
            public byte[] preparedTypeInfo;

            public CachedTypeWriter(JsonSerializer serializer, Type handlerType)
            {
                this.serializer = serializer;
                this.writer = serializer.writer;                
                this.handlerType = handlerType;
            }

            public bool NoRefTypes => noRefTypes;

            public Type HandlerType => handlerType;

            public void SetItemWriter<T>(Action<T, bool, ByteSegment> itemWriter, bool childrenMustWriteRefPath)
            {
                this.handlerType = typeof(T);
                this.noRefTypes = !childrenMustWriteRefPath && this.handlerType.IsValueType;
                this.itemWriter = itemWriter;
                this.objectItemWriter = (item, deviatingType, itemName) => itemWriter.Invoke((T)item, deviatingType, itemName);
            }

            public void SetItemHandler<T>(ItemHandler<T> itemHandler, JsonDataTypeCategory category, Type handlerType)
            {
                switch (category)
                {
                    case JsonDataTypeCategory.Primitive: SetItemHandler_Primitive(itemHandler); break;
                    case JsonDataTypeCategory.Array: SetItemHandler_Array(itemHandler, false); break;
                    case JsonDataTypeCategory.Array_WithoutRefChildren: SetItemHandler_Array(itemHandler, true); break;
                    case JsonDataTypeCategory.Object: SetItemHandler_Object(itemHandler, false); break;
                    case JsonDataTypeCategory.Object_WithoutRefChildren: SetItemHandler_Object(itemHandler, true); break;
                }
                if (!handlerType.IsAssignableTo(typeof(T))) throw new ArgumentException($"The provided item handler for type {typeof(T).FullName} is not compatible with the actual item type {handlerType.FullName}");
                this.handlerType = handlerType;
            }

            public void SetItemHandler_Primitive<T>(ItemHandler<T> itemHandler)
            {
                this.handlerType = typeof(T);
                this.noRefTypes = true;
                Action<T, bool, ByteSegment> temp;
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
                    temp = (item, deviatingType, _) =>
                    {
                        if (!deviatingType)
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
                this.itemWriter = temp;
                this.objectItemWriter = (item, deviatingType, itemName) => temp.Invoke((T)item, deviatingType, itemName);
            }

            public void SetItemHandler_Array<T>(ItemHandler<T> itemHandler, bool noRefChildren)
            {
                this.handlerType = typeof(T);
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<T, bool, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                    {
                        if (!this.handlerType.IsValueType)
                        {
                            temp = (item, _, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item))
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
                            temp = (item, _, itemName) =>
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
                            temp = (item, deviatingType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item))
                                {
                                    Type itemType = item.GetType();
                                    bool writeTypeInfo = serializer.TypeInfoRequired(deviatingType);
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
                            temp = (item, deviatingType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                Type itemType = item.GetType();
                                bool writeTypeInfo = serializer.TypeInfoRequired(deviatingType);
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
                        temp = (item, _, itemName) =>
                        {
                            writer.OpenArray();
                            itemHandler.Invoke(item);
                            writer.CloseArray();
                        };
                    }
                    else
                    {
                        temp = (item, deviatingType, itemName) =>
                        {
                            Type itemType = item.GetType();
                            bool writeTypeInfo = serializer.TypeInfoRequired(deviatingType);
                            if (writeTypeInfo) serializer.StartTypeInfoObject(preparedTypeInfo);
                            writer.OpenArray();
                            itemHandler.Invoke(item);
                            writer.CloseArray();
                            if (writeTypeInfo) serializer.FinishTypeInfoObject();
                        };
                    }
                }
                this.itemWriter = temp;
                this.objectItemWriter = (item, deviatingType, itemName) => temp.Invoke((T)item, deviatingType, itemName);
            }

            public void SetItemHandler_Object<T>(ItemHandler<T> itemHandler, bool noRefChildren)
            {
                this.handlerType = typeof(T);               
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<T, bool, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (!this.handlerType.IsValueType)
                    {
                        if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                        {
                            temp = (item, _, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item))
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
                            temp = (item, deviatingType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item))
                                {
                                    Type itemType = item.GetType();
                                    writer.OpenObject();
                                    int tempBufferCount1 = -1;
                                    int tempBufferCount2 = -1;
                                    if (serializer.TypeInfoRequired(deviatingType))
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
                            temp = (item, _, itemName) =>
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
                            temp = (item, deviatingType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                Type itemType = item.GetType();
                                writer.OpenObject();
                                int tempBufferCount1 = -1;
                                int tempBufferCount2 = -1;
                                if (serializer.TypeInfoRequired(deviatingType))
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
                        temp = (item, _, itemName) =>
                        {
                            writer.OpenObject();
                            itemHandler.Invoke(item);
                            writer.CloseObject();
                        };
                    }
                    else
                    {
                        temp = (item, deviatingType, itemName) =>
                        {
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            int tempBufferCount1 = -1;
                            int tempBufferCount2 = -1;
                            if (serializer.TypeInfoRequired(deviatingType))
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

                this.itemWriter = temp;
                this.objectItemWriter = (item, _, fieldName) => temp.Invoke((T)item, _, fieldName);
            }


            public void SetItemHandler_Object<T>(Action<T>[] fieldHandlers, bool noRefChildren)
            {
                this.handlerType = typeof(T);
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<T, bool, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (!this.handlerType.IsValueType)
                    {
                        if (serializer.settings.typeInfoHandling == TypeInfoHandling.AddNoTypeInfo)
                        {
                            temp = (item, _, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item))
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
                            temp = (item, deviatingType, itemName) =>
                            {
                                serializer.CreateItemInfoForClass(item, itemName);
                                if (!serializer.TryHandleItemAsRef(item))
                                {
                                    Type itemType = item.GetType();
                                    writer.OpenObject();
                                    if (serializer.TypeInfoRequired(deviatingType))
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
                            temp = (item, _, itemName) =>
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
                            temp = (item, deviatingType, itemName) =>
                            {
                                serializer.CreateItemInfoForStruct(itemName);
                                Type itemType = item.GetType();
                                writer.OpenObject();
                                if (serializer.TypeInfoRequired(deviatingType))
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
                        temp = (item, _, itemName) =>
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
                        temp = (item, deviatingType, itemName) =>
                        {
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            if (serializer.TypeInfoRequired(deviatingType))
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

                this.itemWriter = temp;
                this.objectItemWriter = (item, _, fieldName) => temp.Invoke((T)item, _, fieldName);
            }
            public void SetItemHandler_Object_ForNullableStruct<T>(Action<T>[] fieldHandlers, bool noRefChildren) where T : struct
            {
                this.handlerType = typeof(Nullable<T>);
                this.noRefTypes = noRefChildren && this.handlerType.IsValueType;
                Action<Nullable<T>, bool, ByteSegment> temp;
                if (serializer.settings.requiresItemInfos)
                {
                    if (serializer.settings.typeInfoHandling != TypeInfoHandling.AddAllTypeInfo)
                    {
                        temp = (nullableItem, _, itemName) =>
                        {
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
                        temp = (nullableItem, deviatingType, itemName) =>
                        {
                            T item = nullableItem.Value;
                            serializer.CreateItemInfoForStruct(itemName);
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            if (serializer.TypeInfoRequired(deviatingType))
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
                    if (serializer.settings.typeInfoHandling != TypeInfoHandling.AddAllTypeInfo)
                    {
                        temp = (nullableItem, _, _) =>
                        {
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
                        temp = (nullableItem, deviatingType, _) =>
                        {
                            T item = nullableItem.Value;
                            Type itemType = item.GetType();
                            writer.OpenObject();
                            if (serializer.TypeInfoRequired(deviatingType))
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

                this.itemWriter = temp;
                this.objectItemWriter = (item, _, fieldName) => temp.Invoke((T)item, _, fieldName);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteItem<T>(T item, ByteSegment fieldName)
            {
                if (item == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                Type callType = typeof(T);
                if (callType == handlerType)
                {
                    Action<T, bool, ByteSegment> typedItemWriter = (Action<T, bool, ByteSegment>)itemWriter;
                    typedItemWriter.Invoke(item, false, fieldName);
                }
                else
                {
                    objectItemWriter(item, true, fieldName);
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
