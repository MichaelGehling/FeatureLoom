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
            private Delegate populatingItemReader;
            private Func<object> objectItemReader;
            private Type readerType;
            private bool isRefType;
            private JsonDataTypeCategory category;
            private readonly bool enableReferenceResolution;
            private readonly bool enableProposedTypes;

            public JsonDataTypeCategory JsonTypeCategory => category;

            public Type ReaderType => readerType;

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

            public void SetPopulatingTypeReader<T>(Func<T, T> typeReader, JsonDataTypeCategory category)
            {
                this.readerType = typeof(T);
                this.category = category;
                this.isRefType = !readerType.IsValueType;
                bool isNullable = readerType.IsNullable();
                Func<T,T> temp;
                if (isNullable)
                {
                    if (isRefType && enableReferenceResolution)
                    {
                        temp = (item) =>
                        {
                            deserializer.SkipWhiteSpaces();
                            if (deserializer.TryReadNullValue()) return default;
                            if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                            return typeReader.Invoke(item);
                        };
                    }
                    else
                    {
                        temp = (item) =>
                        {
                            deserializer.SkipWhiteSpaces();
                            if (deserializer.TryReadNullValue()) return default;
                            return typeReader.Invoke(item);
                        };
                    }

                }
                else
                {
                    temp = typeReader;
                }
                this.populatingItemReader = temp;
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

            public T ReadValue<T>(ByteSegment itemName, T itemToPopulate)
            {
                if (this.populatingItemReader == null || itemToPopulate == null) return ReadValue<T>(itemName);

                if (category == JsonDataTypeCategory.Primitive)
                {
                    return ReadItem<T>();
                }
                else if (!enableReferenceResolution)
                {
                    return ReadItem<T>(itemToPopulate);
                }
                else
                {
                    ItemInfo myItemInfo = new ItemInfo(itemName, deserializer.currentItemInfoIndex);
                    deserializer.currentItemInfoIndex = deserializer.itemInfos.Count;
                    deserializer.itemInfos.Add(myItemInfo);

                    T result = ReadItem<T>(itemToPopulate);

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
            public T ReadItem<T>(T itemToPopulate)
            {
                if (this.populatingItemReader == null || itemToPopulate == null) return ReadItem<T>();

                if (enableProposedTypes && deserializer.TryReadAsProposedType(this, itemToPopulate, out T item)) return item;

                Type itemType = itemToPopulate.GetType();
                T result;
                if (itemType == this.readerType)
                {
                    Func<T,T> typedItemReader = (Func<T,T>)populatingItemReader;
                    result = typedItemReader.Invoke(itemToPopulate);
                }
                else
                {
                    var typedReader = deserializer.GetCachedTypeReader(itemType);
                    result = typedReader.ReadItem(itemToPopulate);
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T ReadItemIgnoreProposedType<T>(T itemToPopulate)
            {
                if (this.populatingItemReader == null || itemToPopulate == null) return ReadItemIgnoreProposedType<T>();

                Type itemType = itemToPopulate.GetType();
                T result;
                if (itemType == this.readerType)
                {
                    Func<T, T> typedItemReader = (Func<T, T>)populatingItemReader;
                    result = typedItemReader.Invoke(itemToPopulate);
                }
                else
                {
                    var typedReader = deserializer.GetCachedTypeReader(itemType);
                    result = typedReader.ReadItemIgnoreProposedType(itemToPopulate);
                }

                return result;
            }
        }
    }
}
