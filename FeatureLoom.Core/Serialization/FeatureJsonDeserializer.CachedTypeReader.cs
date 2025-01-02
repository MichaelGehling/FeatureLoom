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
            private readonly FeatureJsonDeserializer deserializer;
            private Delegate itemReader;
            private Func<object> objectItemReader;
            private Type readerType;
            private bool isRefType;
            private JsonDataTypeCategory category;
            private readonly bool enableReferenceResolution;
            private readonly bool enableProposedTypes;

            public JsonDataTypeCategory JsonTypeCategory => category;

            public CachedTypeReader(FeatureJsonDeserializer deserializer)
            {
                this.deserializer = deserializer;
                enableReferenceResolution = deserializer.settings.enableReferenceResolution;
                enableProposedTypes = deserializer.settings.enableProposedTypes;
            }            

            public void SetCustomTypeReader<T>(ICustomTypeReader<T> customTypeReader)
            {
                SetTypeReader<T>(() => customTypeReader.ReadValue(deserializer.extensionApi), customTypeReader.JsonTypeCategory);
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
                    if (isRefType && enableReferenceResolution)
                    {
                        temp = () =>
                        {
                            deserializer.SkipWhiteSpaces();
                            if (deserializer.TryReadNullValue()) return default;
                            if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                            return typeReader.Invoke();
                        };
                    }
                    else
                    {
                        temp = () =>
                        {
                            deserializer.SkipWhiteSpaces();
                            if (deserializer.TryReadNullValue()) return default;
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

            public T ReadFieldName<T>(out ByteSegment itemName)
            {
                if (enableReferenceResolution)
                {

                    byte b = deserializer.SkipWhiteSpaces();
                    if (b != '"') throw new Exception("Not a proper field name");                    
                    var recording = deserializer.buffer.StartRecording(true);

                    T result = ReadItemIgnoreProposedType<T>();

                    var bytes = recording.GetRecordedBytes(false);
                    if (bytes[bytes.Count - 1] != '"') throw new Exception("Not a proper field name");
                    itemName = bytes.SubSegment(0, bytes.Count - 1);

                    return result;
                }
                else
                {
                    itemName = default;
                    return ReadItemIgnoreProposedType<T>();
                }
            }

            public T ReadValue<T>(ByteSegment itemName)
            {

                if (!enableReferenceResolution || category == JsonDataTypeCategory.Primitive)
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
                if (enableProposedTypes && deserializer.TryReadAsProposedType(this, out T item)) return item;

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
