using System;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonDeserializer
    {
        sealed class CachedTypeReader
        {
            private FeatureJsonDeserializer deserializer;
            private Delegate itemReader;
            private Func<object> objectItemReader;
            private Type readerType;
            private bool isRefType;
            private JsonDataTypeCategory category;

            public CachedTypeReader(FeatureJsonDeserializer serializer)
            {
                this.deserializer = serializer;
            }

            public void SetTypeReader<T>(Func<T> typeReader, JsonDataTypeCategory category)
            {
                this.readerType = typeof(T);
                this.category = category;
                this.isRefType = !readerType.IsValueType;
                bool isNullable = readerType.IsNullable();
                Func<T> temp;
                if (isNullable)
                {
                    if (isRefType && deserializer.settings.enableReferenceResolution)
                    {
                        temp = () =>
                        {
                            deserializer.SkipWhiteSpaces();
                            if (deserializer.TryReadNull()) return default;
                            if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                            return typeReader.Invoke();
                        };
                    }
                    else
                    {
                        temp = () =>
                        {
                            deserializer.SkipWhiteSpaces();
                            if (deserializer.TryReadNull()) return default;
                            return typeReader.Invoke();
                        };
                    }

                }
                else
                {
                    temp = typeReader;
                }
                this.itemReader = temp;
                this.objectItemReader = () => (object)temp.Invoke();
            }

            public T ReadFieldName<T>(out EquatableByteSegment itemName)
            {
                if (deserializer.settings.enableReferenceResolution)
                {

                    deserializer.SkipWhiteSpaces();
                    if (deserializer.CurrentByte != '"') throw new Exception("Not a proper field name");
                    int itemNameStartPos = deserializer.bufferPos + 1;

                    T result = ReadItemIgnoreProposedType<T>();

                    if (deserializer.buffer[deserializer.bufferPos - 1] != '"') throw new Exception("Not a proper field name");
                    int itemNameLength = deserializer.bufferPos - itemNameStartPos - 1;
                    var nameBuffer = deserializer.tempSlicedBuffer.GetSlice(itemNameLength);
                    nameBuffer.CopyFrom(deserializer.buffer, itemNameStartPos, itemNameLength);
                    itemName = nameBuffer;

                    return result;
                }
                else
                {
                    itemName = default;
                    return ReadItemIgnoreProposedType<T>();
                }
            }

            public T ReadValue<T>(EquatableByteSegment itemName)
            {

                if (!deserializer.settings.enableReferenceResolution || category == JsonDataTypeCategory.Primitive)
                {                    
                    return ReadItem<T>();
                }
                else
                {
                    ItemInfo myItemInfo = new ItemInfo(itemName, deserializer.currentItemInfoIndex);
                    deserializer.currentItemInfoIndex = deserializer.itemInfos.Count;
                    deserializer.itemInfos.Add(myItemInfo);

                    T result = ReadItem<T>();

                    deserializer.currentItemInfoIndex = myItemInfo.parentIndex;
                    return result;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T ReadItem<T>()
            {
                if (deserializer.settings.enableProposedTypes && deserializer.TryReadAsProposedType(this, out T item)) return item;

                Type callType = typeof(T);
                T result;
                if (callType == this.readerType)
                {
                    Func<T> typedItemReader = (Func<T>)itemReader;
                    result = typedItemReader.Invoke();
                }
                else
                {
                    result = (T)objectItemReader.Invoke();
                }

                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T ReadItemIgnoreProposedType<T>()
            {
                Type callType = typeof(T);
                T result;
                if (callType == this.readerType)
                {
                    Func<T> typedItemReader = (Func<T>)itemReader;
                    result = typedItemReader.Invoke();
                }
                else
                {
                    result = (T)objectItemReader.Invoke();
                }

                return result;
            }
        }
    }
}
