using System;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using FeatureLoom.Collections;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{    
    sealed class TypeReaderInitializer
    {
        public FeatureJsonDeserializer parent;
        public Type readerType;
        public Delegate readingDelegate;
        public Delegate populatingDelegate;
        public Func<object> readingObjectDelegate;
        public Func<object, object> populatingObjectDelegate;
        public bool refTypeOrRefTypeChildren;        

        public static TypeReaderInitializer Create<T>(
            FeatureJsonDeserializer parent,
            Func<T> readingDelegate, 
            Func<T, T> populatingDelegate, 
            bool refTypeOrRefTypeChildren)
        {
            var readerType = typeof(T);
            var isNullable = readerType.IsNullable();
            var resolveRefPath = parent.settings.enableReferenceResolution && (refTypeOrRefTypeChildren || !readerType.IsValueType);
            var readingDelegate2 = readingDelegate;
            var populatingDelegate2 = populatingDelegate;
            if (isNullable && resolveRefPath)
            {
                readingDelegate2 = () =>
                {
                    if (parent.TryReadNullValue()) return default;
                    if (parent.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                    return readingDelegate.Invoke();
                };
                if (populatingDelegate != null)
                {
                    populatingDelegate2 = (item) =>
                    {
                        if (isNullable && parent.TryReadNullValue()) return default;
                        if (resolveRefPath && parent.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                        return populatingDelegate.Invoke(item);
                    };
                }
            }
            else if (isNullable)
            {
                readingDelegate2 = () =>
                {
                    if (parent.TryReadNullValue()) return default;
                    return readingDelegate.Invoke();
                };
                if (populatingDelegate != null)
                {
                    populatingDelegate2 = (item) =>
                    {
                        if (isNullable && parent.TryReadNullValue()) return default;
                        return populatingDelegate.Invoke(item);
                    };
                }
            }
            else if (resolveRefPath)
            {
                readingDelegate2 = () =>
                {
                    if (parent.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                    return readingDelegate.Invoke();
                };
                if (populatingDelegate != null)
                {
                    populatingDelegate2 = (item) =>
                    {
                        if (resolveRefPath && parent.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                        return populatingDelegate.Invoke(item);
                    };
                }
            }

            return new TypeReaderInitializer
            {
                parent = parent,
                readerType = typeof(T),
                readingDelegate = readingDelegate2,
                populatingDelegate = populatingDelegate2,
                readingObjectDelegate = readingDelegate2 != null ? () => (object)readingDelegate2.Invoke() : null,
                populatingObjectDelegate = populatingDelegate2 != null ? (obj) => (object)populatingDelegate2.Invoke((T)obj) : null,
                refTypeOrRefTypeChildren = refTypeOrRefTypeChildren
            };
        }
    }


    sealed class CachedTypeReader
    {
        private readonly FeatureJsonDeserializer parent;        

        private readonly Type readerType;
        private readonly bool refTypeOrRefTypeChildren;
        private readonly bool enableProposedTypes;
        private readonly bool isAbstract;
        private readonly bool isNullable;
        private readonly bool writeRefPath;
        private readonly bool resolveRefPath;
        private readonly bool canBePopulated;
        
        private readonly Delegate readingDelegate;
        private readonly Delegate populatingDelegate;
        private readonly Func<object> readingObjectDelegate;
        private readonly Func<object, object> populatingObjectDelegate;

        public bool RefTypeOrRefTypeChildren => refTypeOrRefTypeChildren;
        public Type ReaderType => readerType;
        public bool IsNoCheckPossible<T>() => typeof(T) == readerType && !enableProposedTypes && !resolveRefPath && !writeRefPath;
        public bool CanBePopulated => canBePopulated;

        public CachedTypeReader(Func<CachedTypeReader, TypeReaderInitializer> buildInit)
        {
            var init = buildInit(this);
            parent = init.parent;

            readerType = init.readerType;
            refTypeOrRefTypeChildren = init.refTypeOrRefTypeChildren;
            isAbstract = readerType.IsAbstract;
            isNullable = readerType.IsNullable();
            canBePopulated = init.populatingDelegate != null && init.populatingObjectDelegate != null;            
            resolveRefPath = parent.settings.enableReferenceResolution && !readerType.IsValueType;
            enableProposedTypes = parent.settings.enableProposedTypes;
            writeRefPath = parent.settings.enableReferenceResolution && refTypeOrRefTypeChildren;

            readingDelegate = init.readingDelegate;
            populatingDelegate = init.populatingDelegate;
            readingObjectDelegate = init.readingObjectDelegate;
            populatingObjectDelegate = init.populatingObjectDelegate;            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFieldName<T>(out ByteSegment fieldName)
        {
            if (writeRefPath)
            {
                byte b = parent.SkipWhiteSpaces();
                if (b != '"') throw new Exception("Not a proper field name");                    
                var recording = parent.buffer.StartRecording(true);

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
            if (!writeRefPath)
            {                    
                return ReadValue_CheckProposed<T>();
            }
            else
            {
                ItemInfo myItemInfo = new ItemInfo(fieldName, parent.currentItemInfoIndex);
                parent.currentItemInfoIndex = parent.itemInfos.Count;
                parent.itemInfos.Add(myItemInfo);

                T result = ReadValue_CheckProposed<T>();

                parent.currentItemInfoIndex = myItemInfo.parentIndex;
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFieldValue<T>(ByteSegment fieldName, T itemToPopulate)
        {
            if (!writeRefPath)
            {
                return ReadValue_CheckProposed<T>(itemToPopulate);
            }
            else
            {
                ItemInfo myItemInfo = new ItemInfo(fieldName, parent.currentItemInfoIndex);
                parent.currentItemInfoIndex = parent.itemInfos.Count;
                parent.itemInfos.Add(myItemInfo);

                T result = ReadValue_CheckProposed<T>(itemToPopulate);

                parent.currentItemInfoIndex = myItemInfo.parentIndex;
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
                result = ((Func<T>)readingDelegate).Invoke();
            }
            else
            {
                if (!isAbstract)
                {                                        
                    result = (T)readingObjectDelegate.Invoke();
                }
                else
                {
                    var typedReader = parent.GetCachedTypeReader(callType);
                    result = typedReader.ReadValue_CheckProposed<T>();
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue_CheckProposed<T>(T itemToPopulate)
        {
            if (!canBePopulated || itemToPopulate == null) return ReadValue_CheckProposed<T>();
            if (enableProposedTypes && TryReadAsProposedType(this, itemToPopulate, out T item)) return item;

            Type itemType = itemToPopulate.GetType();
            T result;
            if (itemType == this.readerType)
            {                
                Type callType = typeof(T);
                if (callType == this.readerType)
                {                    
                    result = ((Func<T, T>)populatingDelegate).Invoke(itemToPopulate);
                }
                else
                {                    
                    result = (T)populatingObjectDelegate.Invoke(itemToPopulate);
                }
            }
            else
            {
                var typedReader = parent.GetCachedTypeReader(itemType);
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
                result = ((Func<T>)readingDelegate).Invoke();
            }
            else
            {
                if (!isAbstract)
                {                    
                    result = (T)readingObjectDelegate.Invoke();
                }
                else
                {
                    var typedReader = parent.GetCachedTypeReader(callType);
                    result = typedReader.ReadValue_IgnoreProposed<T>();
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue_IgnoreProposed<T>(T itemToPopulate)
        {
            if (!canBePopulated || itemToPopulate == null) return ReadValue_IgnoreProposed<T>();

            Type itemType = itemToPopulate.GetType();
            T result;
            if (itemType == this.readerType)
            {                
                Type callType = typeof(T);                

                if (callType == this.readerType)
                {                    
                    result = ((Func<T, T>)populatingDelegate).Invoke(itemToPopulate);
                }
                else
                {                    
                    result = (T)populatingObjectDelegate.Invoke(itemToPopulate);
                }
            }
            else
            {
                var typedReader = parent.GetCachedTypeReader(itemType);
                result = typedReader.ReadValue_IgnoreProposed(itemToPopulate);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadValue_NoCheck<T>()
        {
            return ((Func<T>)readingDelegate).Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue_NoCheck<T>(T itemToPopulate)
        {
            if (!canBePopulated || itemToPopulate == null) return ReadValue_IgnoreProposed<T>();

            Type itemType = itemToPopulate.GetType();
            T result;
            if (itemType == this.readerType)
            {
                Type callType = typeof(T);

                if (callType == this.readerType)
                {
                    result = ((Func<T, T>)populatingDelegate).Invoke(itemToPopulate);
                }
                else
                {
                    result = (T)populatingObjectDelegate.Invoke(itemToPopulate);
                }
            }
            else
            {
                var typedReader = parent.GetCachedTypeReader(itemType);
                result = typedReader.ReadValue_IgnoreProposed(itemToPopulate);
            }

            return result;
        }

        bool TryReadAsProposedType<T>(CachedTypeReader originalTypeReader, out T item)
        {
            item = default;
            byte b = parent.SkipWhiteSpaces();
            // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
            // so we can skip the rest of this method and just read it as the original type
            if (b != (byte)'{') return false;

            CachedTypeReader proposedTypeReader = null;
            bool foundValueField = false;

            var undoHandle = parent.CreateUndoReadHandle();
            { 
                if (!parent.TryFindProposedType(out proposedTypeReader, typeof(T), out foundValueField))
                {
                    if (!foundValueField)
                    {
                        undoHandle.Dispose();
                        return false;
                    }
                }
                undoHandle.SetUndoReading(!foundValueField);
            }
            undoHandle.Dispose();

            if (foundValueField)
            {
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (proposedTypeReader != null)
                {
                    item = proposedTypeReader.ReadValue_IgnoreProposed<T>();
                }
                else item = originalTypeReader.ReadValue_IgnoreProposed<T>();
                parent.SkipRemainingFieldsOfObject();
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
            byte b = parent.SkipWhiteSpaces();
            // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
            if (b != (byte)'{') return false;

            CachedTypeReader proposedTypeReader = null;
            bool foundValueField = false;
            var undoHandle = parent.CreateUndoReadHandle();
            {
                if (!parent.TryFindProposedType(out proposedTypeReader, typeof(T), out foundValueField))
                {
                    if (!foundValueField)
                    {
                        undoHandle.Dispose();
                        return false;
                    }
                }
                undoHandle.SetUndoReading(!foundValueField);
            }
            undoHandle.Dispose();

            if (foundValueField)
            {
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (proposedTypeReader != null)
                {
                    if (itemToPopulate.GetType().IsAssignableTo(proposedTypeReader.ReaderType)) item = proposedTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                    else item = proposedTypeReader.ReadValue_IgnoreProposed<T>();
                }
                else item = originalTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                parent.SkipRemainingFieldsOfObject();
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
