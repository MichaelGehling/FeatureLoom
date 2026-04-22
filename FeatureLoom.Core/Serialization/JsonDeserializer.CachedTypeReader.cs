using System;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using FeatureLoom.Collections;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{    
    sealed class TypeReaderInitializer
    {
        public JsonDeserializer deserializer;
        public Type readerType;
        public Delegate readingDelegate;
        public Delegate populatingDelegate;
        public Func<object> readingObjectDelegate;
        public Func<object, object> populatingObjectDelegate;        
        public BaseTypeSettings typeSettings;
        public bool resolveRefPath;
        public bool writeRefPath;
        public bool overridePopulateExistingMembers;

        public static TypeReaderInitializer Create<T>(
            JsonDeserializer deserializer,
            Func<T> readingDelegate, 
            Func<T, T> populatingDelegate, 
            bool childrenMustWriteRefPath,
            BaseTypeSettings typeSettings)
        {
            var readerType = typeof(T);

            bool writeRefPath = false;
            bool resolveRefPath = false;
            if (deserializer.settings.referenceResolutionMode != Settings.ReferenceResolutionMode.ForceDisabled)
            {
                if (readerType == typeof(string))
                {
                    writeRefPath = deserializer.settings.referenceResolutionMode == Settings.ReferenceResolutionMode.EnabledByDefaultPlusStrings || typeSettings?.enableReferenceResolution == true;
                    resolveRefPath = deserializer.settings.referenceResolutionMode == Settings.ReferenceResolutionMode.EnabledByDefaultPlusStrings || typeSettings?.enableReferenceResolution == true;
                }
                else
                {
                    if (deserializer.settings.referenceResolutionMode == Settings.ReferenceResolutionMode.OnlyPerType)
                    {
                        writeRefPath = childrenMustWriteRefPath || (!readerType.IsValueType && typeSettings?.enableReferenceResolution == true);
                        resolveRefPath = !readerType.IsValueType && typeSettings?.enableReferenceResolution == true;
                    }
                    else
                    {
                        writeRefPath = childrenMustWriteRefPath || (!readerType.IsValueType && typeSettings?.enableReferenceResolution != false);
                        resolveRefPath = !readerType.IsValueType && typeSettings?.enableReferenceResolution != false;
                    }
                }
            }

            if (typeSettings?.populateAsMember == false) populatingDelegate = null;

            var readingDelegate2 = readingDelegate;
            var populatingDelegate2 = populatingDelegate;            
            Func<object> readingObjectDelegate = readingDelegate2 != null ? () => (object)readingDelegate2.Invoke() : null;
            Func<object, object> populatingObjectDelegate = populatingDelegate2 != null ? (obj) => (object)populatingDelegate2.Invoke((T)obj) : null;
            if (resolveRefPath)
            {
                readingDelegate2 = () =>
                {
                    if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                    return readingDelegate.Invoke();
                };
                readingObjectDelegate = () =>
                {
                    if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return (object)refObject;
                    return (object) readingDelegate.Invoke();
                };
                if (populatingDelegate != null)
                {
                    populatingDelegate2 = (item) =>
                    {
                        if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return refObject;
                        return populatingDelegate.Invoke(item);
                    };
                    populatingObjectDelegate = (obj) =>
                    {
                        if (deserializer.TryReadRefObject(out bool validPath, out bool compatibleType, out T refObject) && validPath && compatibleType) return (object)refObject;
                        return (object)populatingDelegate.Invoke((T)obj);
                    };
                }
            }

            return new TypeReaderInitializer
            {
                deserializer = deserializer,
                readerType = typeof(T),
                readingDelegate = readingDelegate2,
                populatingDelegate = populatingDelegate2,
                readingObjectDelegate = readingObjectDelegate,
                populatingObjectDelegate = populatingObjectDelegate,
                typeSettings = typeSettings,
                writeRefPath = writeRefPath,
                resolveRefPath = resolveRefPath,
                overridePopulateExistingMembers = populatingDelegate2 != null && typeSettings?.populateAsMember == true
            };
        }
    }


    sealed class CachedTypeReader
    {
        private readonly JsonDeserializer deserializer;        

        private readonly Type readerType;
        private readonly bool checkProposedTypes;
        private readonly bool isAbstract;
        private readonly bool isNullable;
        private readonly bool writeRefPath;
        private readonly bool resolveRefPath;
        private readonly bool canBePopulated;
        private readonly bool overridePopulateExistingMembers;
        private readonly BaseTypeSettings typeSettings;

        private readonly Delegate readingDelegate;
        private readonly Delegate populatingDelegate;
        private readonly Func<object> readingObjectDelegate;
        private readonly Func<object, object> populatingObjectDelegate;

        private ByteSegment lastProposedTypeName = default;
        private CachedTypeReader lastProposedTypeReader = null;

        public Type ReaderType => readerType;
        public bool IsNoCheckPossible<T>() => typeof(T) == readerType && !checkProposedTypes && !resolveRefPath && !writeRefPath;
        public bool CanBePopulated => canBePopulated;

        public bool WriteRefPath => writeRefPath;
        public bool ResolveRefPath => resolveRefPath;
        public BaseTypeSettings TypeSettings => typeSettings;

        public JsonDeserializer Parent => deserializer;

        public CachedTypeReader(Func<CachedTypeReader, TypeReaderInitializer> buildInit)
        {
            var init = buildInit(this);
            deserializer = init.deserializer;
            typeSettings = init.typeSettings;

            readerType = init.readerType;
            writeRefPath = init.writeRefPath;
            isAbstract = readerType.IsAbstract;
            isNullable = readerType.IsNullable();
            canBePopulated = init.populatingDelegate != null && init.populatingObjectDelegate != null;
            overridePopulateExistingMembers = init.overridePopulateExistingMembers && canBePopulated;            

            resolveRefPath = init.resolveRefPath;

            if (typeSettings?.applyProposedTypes == null)
            {
                checkProposedTypes = deserializer.settings.proposedTypeMode == Settings.ProposedTypeMode.CheckAlways ||
                                    (deserializer.settings.proposedTypeMode == Settings.ProposedTypeMode.CheckWhereReasonable && !readerType.IsValueType && !readerType.IsSealed);
            }
            else
            {
                checkProposedTypes = typeSettings.applyProposedTypes.Value;
            }

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
            if (!writeRefPath)
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
            if (!writeRefPath)
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
            if (checkProposedTypes && TryReadAsProposedType(this, out T item)) return item;

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
                    var typedReader = deserializer.GetCachedTypeReader(callType);
                    result = typedReader.ReadValue_CheckProposed<T>();
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadValue_CheckProposed<T>(T itemToPopulate)
        {
            if (SkipPopulate(itemToPopulate)) return ReadValue_CheckProposed<T>();
            if (checkProposedTypes && TryReadAsProposedType(this, itemToPopulate, out T item)) return item;

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
                var typedReader = deserializer.GetCachedTypeReader(itemType);
                result = typedReader.ReadValue_CheckProposed(itemToPopulate);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SkipPopulate<T>(T itemToPopulate)
        {            
            bool doPopulate = canBePopulated && (deserializer.isPopulating || overridePopulateExistingMembers) && itemToPopulate != null;
            return !doPopulate;
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
                    var typedReader = deserializer.GetCachedTypeReader(callType);
                    result = typedReader.ReadValue_IgnoreProposed<T>();
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValue_IgnoreProposed<T>(T itemToPopulate)
        {
            if (SkipPopulate(itemToPopulate)) return ReadValue_IgnoreProposed<T>();

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
                var typedReader = deserializer.GetCachedTypeReader(itemType);
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
        public T ReadValue_NoCheck<T>(T itemToPopulate)
        {
            if (SkipPopulate(itemToPopulate)) return ReadValue_NoCheck<T>();

            Type itemType = itemToPopulate.GetType();
            T result;
            if (itemType == this.readerType)
            {
                result = ((Func<T, T>)populatingDelegate).Invoke(itemToPopulate);
            }
            else
            {
                var typedReader = deserializer.GetCachedTypeReader(itemType);
                result = typedReader.ReadValue_IgnoreProposed(itemToPopulate);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryReadAsProposedType<T>(CachedTypeReader originalTypeReader, out T item)
        {
            item = default;
            byte b = deserializer.SkipWhiteSpaces();
            // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
            // so we can skip the rest of this method and just read it as the original type
            if (b != (byte)'{') return false;            

            bool foundProposedReader = false;
            bool foundValueField = false;

            var undoHandle = deserializer.CreateUndoReadHandle();
            foundProposedReader = deserializer.TryFindProposedType(ref lastProposedTypeReader, ref lastProposedTypeName, typeof(T), out foundValueField);
            if (!foundProposedReader && !foundValueField)
            {
                undoHandle.Dispose();
                return false;
            }
            undoHandle.SetUndoReading(!foundValueField);
            undoHandle.Dispose();

            if (foundValueField)
            {
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (foundProposedReader) item = lastProposedTypeReader.ReadValue_IgnoreProposed<T>();                
                else item = originalTypeReader.ReadValue_IgnoreProposed<T>();
                deserializer.SkipRemainingFieldsOfObject();
            }
            else
            {
                // we read the object again from the start, because the $type field was embedded in the actual value's object,
                // the buffer pos was already reset by the undo handle, so we can just read the item again
                item = lastProposedTypeReader.ReadValue_IgnoreProposed<T>();
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryReadAsProposedType<T>(CachedTypeReader originalTypeReader, T itemToPopulate, out T item)
        {
            item = default;
            byte b = deserializer.SkipWhiteSpaces();
            // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
            if (b != (byte)'{') return false;

            bool foundProposedReader = false;
            bool foundValueField = false;

            var undoHandle = deserializer.CreateUndoReadHandle();
            foundProposedReader = deserializer.TryFindProposedType(ref lastProposedTypeReader, ref lastProposedTypeName, typeof(T), out foundValueField);
            if (!foundProposedReader && !foundValueField)
            {
                undoHandle.Dispose();
                return false;
            }
            undoHandle.SetUndoReading(!foundValueField);
            undoHandle.Dispose();

            if (foundValueField)
            {
                // bufferPos is currently at the position of the actual value, so read on from here, but handle the rest of the type object afterwards
                if (foundProposedReader)
                {
                    if (itemToPopulate.GetType().IsAssignableTo(lastProposedTypeReader.ReaderType)) item = lastProposedTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                    else item = lastProposedTypeReader.ReadValue_IgnoreProposed<T>();
                }
                else item = originalTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                deserializer.SkipRemainingFieldsOfObject();
            }
            else
            {
                // we read the object again from the start, because the $type field was embedded in the actual value's object,
                // the buffer pos was already reset by the undo handle, so we can just read the item again,
                // but we have to check if the proposed type is compatible with the item to populate, otherwise we would populate the wrong type of object
                if (itemToPopulate.GetType().IsAssignableTo(lastProposedTypeReader.ReaderType)) item = lastProposedTypeReader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                else item = lastProposedTypeReader.ReadValue_IgnoreProposed<T>();
            }
            return true;
        }
    }
}
