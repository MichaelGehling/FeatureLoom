using System;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace Playground
{
    public sealed partial class FeatureJsonDeserializer
    {
        sealed class CachedTypeReader
        {
            private FeatureJsonDeserializer deserializer;
            private Delegate itemReader;
            private Func<object> objectItemReader;
            private Type readerType;

            public CachedTypeReader(FeatureJsonDeserializer serializer)
            {
                this.deserializer = serializer;
            }

            public void SetTypeReader<T>(Func<T> typeReader, JsonDataTypeCategory category)
            {
                this.readerType = typeof(T);                                
                bool isNullable = readerType.IsNullable();
                Func<T> temp;
                if (isNullable)
                {
                    temp = () =>
                    {
                        deserializer.SkipWhiteSpaces();
                        if (deserializer.PeekNull()) return default;                            
                        return typeReader.Invoke();
                    };
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
                deserializer.SkipWhiteSpaces();
                if (deserializer.CurrentByte != '"') throw new Exception("Not a proper field name");
                int itemNameStartPos = deserializer.bufferPos + 1;

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

                if (deserializer.buffer[deserializer.bufferPos - 1] != '"') throw new Exception("Not a proper field name");
                int itemNameLength = deserializer.bufferPos - itemNameStartPos - 1;
                var nameBuffer = deserializer.tempSlicedBuffer.GetSlice(itemNameLength);
                nameBuffer.CopyFrom(deserializer.buffer, itemNameStartPos, itemNameLength);
                itemName = nameBuffer;
                return result;
            }

            public T ReadValue<T>(EquatableByteSegment itemName)
            {
                ItemInfo myItemInfo = new ItemInfo(itemName, deserializer.currentItemInfoIndex);
                deserializer.currentItemInfoIndex = deserializer.itemInfos.Count;
                deserializer.itemInfos.Add(myItemInfo);

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

                deserializer.currentItemInfoIndex = myItemInfo.parentIndex;
                return result;
            }

            public T ReadItem<T>()
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
