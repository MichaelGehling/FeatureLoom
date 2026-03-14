using System;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using FeatureLoom.Collections;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{
    sealed class CachedTypeReader
    {
        private readonly FeatureJsonDeserializer deserializer;
        private Delegate itemReader;
        private Delegate populatingItemReader;
        private Func<object> objectItemReader;
        private Func<object, object> populatingObjectItemReader;
        private Type readerType;
        private bool isRefType;
        private JsonDataTypeCategory category;
        private readonly bool enableReferenceResolution;
        private readonly bool enableProposedTypes;
        private bool isAbstract = false;
        private StringRepresentation stringRepresentation;

        public JsonDataTypeCategory JsonTypeCategory => category;

        public Type ReaderType => readerType;

        public CachedTypeReader(FeatureJsonDeserializer deserializer)
        {
            this.deserializer = deserializer;
            enableReferenceResolution = deserializer.settings.enableReferenceResolution;
            enableProposedTypes = deserializer.settings.enableProposedTypes;
        }            

        public void MakeAbstract<T>(JsonDataTypeCategory category)
        {
            isAbstract = true;
            this.readerType = typeof(T);
            this.category = category;
            this.isRefType = !readerType.IsValueType;
            bool isNullable = readerType.IsNullable();
        }

        public void SetCustomTypeReader<T>(ICustomTypeReader<T> customTypeReader)
        {
            SetTypeReader<T>(() => customTypeReader.ReadValue(deserializer.extensionApi), customTypeReader.JsonTypeCategory, customTypeReader.StringRepresentation);
        }

        public void SetTypeReader<T>(Func<T> typeReader, JsonDataTypeCategory category, StringRepresentation stringRepresentation)
        {
            this.readerType = typeof(T);
            this.category = category;
            this.isRefType = !readerType.IsValueType;
            bool isNullable = readerType.IsNullable();
            this.stringRepresentation = stringRepresentation;

            Func<T> temp;
            if (isNullable)
            {
                if (isRefType && enableReferenceResolution)
                {
                    temp = () =>
                    {
                        if (deserializer.TryReadNullValue()) return default;
                        if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                        return typeReader.Invoke();
                    };
                }
                else
                {
                    temp = () =>
                    {
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
                        if (deserializer.TryReadNullValue()) return default;
                        if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                        return typeReader.Invoke(item);
                    };
                }
                else
                {
                    temp = (item) =>
                    {
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
            this.populatingObjectItemReader = (item) => (object)temp.Invoke((T)item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFieldName<T>(out ByteSegment fieldName)
        {
            if (enableReferenceResolution)
            {

                byte b = deserializer.SkipWhiteSpaces();
                if (b != '"') throw new Exception("Not a proper field name");                    
                var recording = deserializer.buffer.StartRecording(true);

                T result = ReadValue_IgnoreProposed<T>();

                var bytes = recording.GetRecordedBytes(false);
                if (bytes[bytes.Count - 1] != '"') throw new Exception("Not a proper field name");
                fieldName = bytes.SubSegment(0, bytes.Count - 1);

                return result;
            }
            else
            {
                fieldName = default;
                return ReadValue_IgnoreProposed<T>();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFieldValue<T>(ByteSegment fieldName)
        {

            if (!enableReferenceResolution || category == JsonDataTypeCategory.Primitive)
            {                    
                return ReadValue_CheckProposed<T>();
            }
            else
            {
                ItemInfo myItemInfo = new ItemInfo(fieldName, deserializer.currentItemInfoIndex);
                deserializer.currentItemInfoIndex = deserializer.itemInfos.Count;
                deserializer.itemInfos.Add(myItemInfo);

                T result = ReadValue_CheckProposed<T>();

                deserializer.currentItemInfoIndex = myItemInfo.parentIndex;
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFieldValue<T>(ByteSegment fieldName, T itemToPopulate)
        {
            if (!deserializer.isPopulating ||
                itemToPopulate == null ||
                (this.populatingItemReader == null && !isAbstract)) return ReadFieldValue<T>(fieldName);

            if (category == JsonDataTypeCategory.Primitive)
            {
                return ReadValue_CheckProposed<T>();
            }
            else if (!enableReferenceResolution)
            {
                return ReadValue_CheckProposed<T>(itemToPopulate);
            }
            else
            {
                ItemInfo myItemInfo = new ItemInfo(fieldName, deserializer.currentItemInfoIndex);
                deserializer.currentItemInfoIndex = deserializer.itemInfos.Count;
                deserializer.itemInfos.Add(myItemInfo);

                T result = ReadValue_CheckProposed<T>(itemToPopulate);

                deserializer.currentItemInfoIndex = myItemInfo.parentIndex;
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadValue_CheckProposed<T>()
        {
            if (enableProposedTypes && TryReadAsProposedType(this, out T item)) return item;

            Type callType = typeof(T);
            T result;                
            if (callType == this.readerType)
            {
                if (isAbstract) throw new Exception($"Can't deserialize abstract type {this.readerType.Name}");

                Func<T> typedItemReader = (Func<T>)itemReader;
                result = typedItemReader.Invoke();
            }
            else
            {
                if (!isAbstract) result = (T)objectItemReader.Invoke();
                else
                {
                    var typedReader = deserializer.GetCachedTypeReader(callType);
                    result = typedReader.ReadValue_CheckProposed<T>();
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue_CheckProposed<T>(T itemToPopulate)
        {
            if ((this.populatingItemReader == null && !isAbstract) || itemToPopulate == null) return ReadValue_CheckProposed<T>();

            if (enableProposedTypes && TryReadAsProposedType(this, itemToPopulate, out T item)) return item;

            Type itemType = itemToPopulate.GetType();
            T result;
            if (itemType == this.readerType)
            {
                if (isAbstract) throw new Exception($"Can't deserialize abstract type {this.readerType.Name}");
                Type callType = typeof(T);
                if (callType == this.readerType)
                {
                    Func<T, T> typedItemReader = (Func<T, T>)populatingItemReader;
                    result = typedItemReader.Invoke(itemToPopulate);
                }
                else
                {
                    result = (T)populatingObjectItemReader.Invoke(itemToPopulate);
                }
            }
            else
            {
                var typedReader = deserializer.GetCachedTypeReader(itemType);
                result = typedReader.ReadValue_CheckProposed(itemToPopulate);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue_IgnoreProposed<T>()
        {
            Type callType = typeof(T);
            T result;
            if (callType == this.readerType)
            {
                if (isAbstract) throw new Exception($"Can't deserialize abstract type {this.readerType.Name}");

                Func<T> typedItemReader = (Func<T>)itemReader;
                result = typedItemReader.Invoke();
            }
            else
            {                    
                if (!isAbstract) result = (T)objectItemReader.Invoke();
                else
                {
                    var typedReader = deserializer.GetCachedTypeReader(callType);
                    result = typedReader.ReadValue_IgnoreProposed<T>();
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue_IgnoreProposed<T>(T itemToPopulate)
        {
            if ((this.populatingItemReader == null && !isAbstract) || itemToPopulate == null) return ReadValue_IgnoreProposed<T>();

            Type itemType = itemToPopulate.GetType();
            T result;
            if (itemType == this.readerType)
            {
                if (isAbstract) throw new Exception($"Can't deserialize abstract type {this.readerType.Name}");
                Type callType = typeof(T);
                if (callType == this.readerType)
                {
                    Func<T, T> typedItemReader = (Func<T, T>)populatingItemReader;
                    result = typedItemReader.Invoke(itemToPopulate);
                }
                else
                {
                    result = (T)populatingObjectItemReader.Invoke(itemToPopulate);
                }
            }
            else
            {
                var typedReader = deserializer.GetCachedTypeReader(itemType);
                result = typedReader.ReadValue_IgnoreProposed(itemToPopulate);
            }

            return result;
        }

        bool TryReadAsProposedType<T>(CachedTypeReader originalTypeReader, out T item)
        {
            item = default;
            byte b = deserializer.SkipWhiteSpaces();
            // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
            // so we can skip the rest of this method and just read it as the original type
            if (b != (byte)'{') return false;

            CachedTypeReader proposedTypeReader = null;
            bool foundValueField = false;
            using (var undoHandle = deserializer.CreateUndoReadHandle())
            {
                if (!deserializer.TryFindProposedType(out proposedTypeReader, typeof(T), out foundValueField))
                {
                    if (!foundValueField) return false;
                }
                undoHandle.SetUndoReading(!foundValueField);
            }

            if (foundValueField)
            {
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (proposedTypeReader != null)
                {
                    item = proposedTypeReader.ReadValue_IgnoreProposed<T>();
                }
                else item = originalTypeReader.ReadValue_IgnoreProposed<T>();
                deserializer.SkipRemainingFieldsOfObject();
            }
            else
            {
                // we read the object again from the start, because the $type field was embedded in the actual value's object,
                // the buffer pos was already reset by the undo handle, so we can just read the item again
                item = proposedTypeReader.ReadValue_IgnoreProposed<T>();
            }
            return true;
        }

        bool TryReadAsProposedType<T>(CachedTypeReader originalTypeReader, T itemToPopulate, out T item)
        {
            item = default;
            byte b = deserializer.SkipWhiteSpaces();
            // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
            if (b != (byte)'{') return false;

            CachedTypeReader proposedTypeReader = null;
            bool foundValueField = false;
            using (var undoHandle = deserializer.CreateUndoReadHandle())
            {
                if (!deserializer.TryFindProposedType(out proposedTypeReader, typeof(T), out foundValueField))
                {
                    if (!foundValueField) return false;
                }
                undoHandle.SetUndoReading(!foundValueField);
            }

            if (foundValueField)
            {
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (proposedTypeReader != null)
                {
                    if (itemToPopulate.GetType().IsAssignableTo(proposedTypeReader.ReaderType)) item = proposedTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                    else item = proposedTypeReader.ReadValue_IgnoreProposed<T>();
                }
                else item = originalTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                deserializer.SkipRemainingFieldsOfObject();
            }
            else
            {
                // we read the object again from the start, because the $type field was embedded in the actual value's object,
                // the buffer pos was already reset by the undo handle, so we can just read the item again,
                // but we have to check if the proposed type is compatible with the item to populate, otherwise we would populate the wrong type of object
                if (itemToPopulate.GetType().IsAssignableTo(proposedTypeReader.ReaderType)) item = proposedTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                else item = proposedTypeReader.ReadValue_IgnoreProposed<T>();
            }
            return true;
        }
    }
}
