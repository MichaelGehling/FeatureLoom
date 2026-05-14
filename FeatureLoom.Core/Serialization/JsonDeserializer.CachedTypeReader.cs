using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using System;
using System.Runtime.CompilerServices;
using System.Text;

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
        public bool resolveRefs;
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
            bool resolveRefs = false;
            if (deserializer.settings.referenceResolutionMode != Settings.ReferenceResolutionMode.ForceDisabled)
            {
                if (readerType == typeof(string))
                {
                    writeRefPath = false;
                    resolveRefs = false;
                }
                else
                {
                    if (deserializer.settings.referenceResolutionMode == Settings.ReferenceResolutionMode.DisabledByDefault)
                    {
                        resolveRefs = !readerType.IsValueType && typeSettings?.enableReferenceResolution == true;
                        writeRefPath = childrenMustWriteRefPath || resolveRefs; 
                    }
                    else
                    {
                        resolveRefs = !readerType.IsValueType && typeSettings?.enableReferenceResolution != false;
                        writeRefPath = childrenMustWriteRefPath || resolveRefs;
                    }
                }
            }

            if (typeSettings?.populateAsMember == false) populatingDelegate = null;
         
            if (readingDelegate == null)
            {
                readingDelegate = () => throw new Exception("Cannot deserialize abstract/interface type ... without mapped or proposed concrete type.");
            }
            Func<object> readingObjectDelegate = () => (object)readingDelegate.Invoke();
            Func<object, object> populatingObjectDelegate = populatingDelegate != null ? (obj) => (object)populatingDelegate.Invoke((T)obj) : null;

            return new TypeReaderInitializer
            {
                deserializer = deserializer,
                readerType = typeof(T),
                readingDelegate = readingDelegate,
                populatingDelegate = populatingDelegate,
                readingObjectDelegate = readingObjectDelegate,
                populatingObjectDelegate = populatingObjectDelegate,
                typeSettings = typeSettings,
                writeRefPath = writeRefPath,
                resolveRefs = resolveRefs,
                overridePopulateExistingMembers = populatingDelegate != null && typeSettings?.populateAsMember == true
            };
        }
    }

    sealed class TypeReaderPreInitializer
    {
        public JsonDeserializer deserializer;
        public Type readerType;
        public BaseTypeSettings typeSettings;
        public bool resolveRefs;
        public bool writeRefPath;
        public bool overridePopulateExistingMembers;

        public TypeReaderPreInitializer(JsonDeserializer deserializer, Type readerType, BaseTypeSettings typeSettings)
        {
            this.deserializer = deserializer;
            this.readerType = readerType;
            this.typeSettings = typeSettings;
            bool writeRefPath = false;
            bool resolveRefs = false;
            if (deserializer.settings.referenceResolutionMode != Settings.ReferenceResolutionMode.ForceDisabled)
            {
                if (readerType == typeof(string))
                {
                    writeRefPath = false;
                    resolveRefs = false;
                }
                else
                {
                    if (deserializer.settings.referenceResolutionMode == Settings.ReferenceResolutionMode.DisabledByDefault)
                    {
                        resolveRefs = !readerType.IsValueType && typeSettings?.enableReferenceResolution == true;
                        writeRefPath = resolveRefs;                        
                    }
                    else
                    {
                        resolveRefs = !readerType.IsValueType && typeSettings?.enableReferenceResolution != false;
                        writeRefPath = resolveRefs;
                    }
                }
            }            
            this.writeRefPath = writeRefPath;
            this.resolveRefs = resolveRefs;
            this.overridePopulateExistingMembers = typeSettings?.populateAsMember == true;
        }
    }


    sealed class CachedTypeReader
    {
        private readonly JsonDeserializer deserializer;        

        private readonly Type readerType;
        private readonly bool checkProposedTypes;
        private readonly bool isAbstract;
        private readonly bool writeRefPath;
        private readonly bool resolveRefs;
        private readonly bool canBePopulated;
        private readonly bool overridePopulateExistingMembers;
        private readonly BaseTypeSettings typeSettings;

        private readonly Delegate readingDelegate;
        private readonly Delegate populatingDelegate;
        private readonly Func<object> readingObjectDelegate;
        private readonly Func<object, object> populatingObjectDelegate;

        private ByteSegment lastProposedTypeName = default;
        private CachedTypeReader lastProposedTypeReader = null;
        private CachedTypeReader lastPopulatedTypeReader = null;

        public Type ReaderType => readerType;
        public bool IsNoCheckPossible<T>() => typeof(T) == readerType && !checkProposedTypes && !resolveRefs && !writeRefPath;
        public bool CanBePopulated => canBePopulated;

        public bool WriteRefPath => writeRefPath;
        public bool ResolveRefs => resolveRefs;
        public bool CheckProposedTypes => checkProposedTypes;

        public CachedTypeReader LastProposedTypeReader { get => lastProposedTypeReader; set => lastProposedTypeReader = value; }
        public ByteSegment LastProposedTypeName { get => lastProposedTypeName; set => lastProposedTypeName = value; }

        public BaseTypeSettings TypeSettings => typeSettings;

        public JsonDeserializer Parent => deserializer;

        public CachedTypeReader(TypeReaderPreInitializer preinit, Func<CachedTypeReader, TypeReaderInitializer> buildInit)
        {
            // This is used for a pre-initialization of the type reader, which is necessary in cases where we have recursive types.            
            if (preinit != null)
            {
                readerType = preinit.readerType;
                deserializer = preinit.deserializer;
                typeSettings = preinit.typeSettings;
                writeRefPath = preinit.writeRefPath;
                resolveRefs = preinit.resolveRefs;
                overridePopulateExistingMembers = preinit.overridePopulateExistingMembers;
                // we have to assume that it can be populated, otherwise we could end up in a situation where the preinit sets populateAsMember to true,
                // but the actual init sets it to false, which would lead to the populating delegate not being generated
                canBePopulated = true; 
            }

            var init = buildInit(this);
            deserializer = init.deserializer;
            typeSettings = init.typeSettings;

            readerType = init.readerType;
            writeRefPath = init.writeRefPath;
            isAbstract = readerType.IsAbstract;
            canBePopulated = init.populatingDelegate != null && init.populatingObjectDelegate != null;
            overridePopulateExistingMembers = init.overridePopulateExistingMembers && canBePopulated;

            resolveRefs = init.resolveRefs;

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

        public CachedTypeReader(Func<CachedTypeReader, TypeReaderInitializer> buildInit) : this(null, buildInit)
        {
         
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFieldName<T>(out ByteSegment fieldName, bool includeFieldNameBytes)
        {
            if (includeFieldNameBytes)
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
            if (!writeRefPath) return ReadValue_CheckProposed<T>();            

            ItemInfo myItemInfo = new ItemInfo(fieldName, deserializer.currentItemInfoIndex);
            deserializer.currentItemInfoIndex = deserializer.itemInfos.Count;
            deserializer.itemInfos.Add(myItemInfo);

            T result = ReadValue_CheckProposed<T>();

            deserializer.currentItemInfoIndex = myItemInfo.parentIndex;
            return result;            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFieldValue<T>(ByteSegment fieldName, T itemToPopulate)
        {            
            if (!writeRefPath) return ReadValue_CheckProposed<T>(itemToPopulate);
            
            ItemInfo myItemInfo = new ItemInfo(fieldName, deserializer.currentItemInfoIndex);
            deserializer.currentItemInfoIndex = deserializer.itemInfos.Count;
            deserializer.itemInfos.Add(myItemInfo);
            
            T result = ReadValue_CheckProposed<T>(itemToPopulate);

            deserializer.currentItemInfoIndex = myItemInfo.parentIndex;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadValue_CheckProposed<T>()
        {            
            if (deserializer.TryReadRefOrProposed(this, out T item)) return item;

            Type callType = typeof(T);
 
            T result;                
            if (callType == this.readerType)
            {                                
                result = ((Func<T>)readingDelegate).Invoke();
            }
            else
            {                   
                result = (T)readingObjectDelegate.Invoke();
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadValue_CheckProposed<T>(T itemToPopulate)
        {
            if (SkipPopulate(itemToPopulate)) return ReadValue_CheckProposed<T>();            
            if (deserializer.TryReadRefOrProposed(this, itemToPopulate, out T item)) return item;

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
                if (lastPopulatedTypeReader?.ReaderType != itemType) lastPopulatedTypeReader = deserializer.GetCachedTypeReader(itemType);
                result = lastPopulatedTypeReader.ReadValue_IgnoreProposed(itemToPopulate);
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
        public T ReadValue_IgnoreProposed<T>()
        {
            Type callType = typeof(T);

            T result;
            if (callType == this.readerType)
            {                                
                result = ((Func<T>)readingDelegate).Invoke();
            }
            else
            {                 
                result = (T)readingObjectDelegate.Invoke();
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadValue_IgnoreProposed<T>(T itemToPopulate)
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
                if (lastPopulatedTypeReader?.ReaderType != itemType) lastPopulatedTypeReader = deserializer.GetCachedTypeReader(itemType);
                result = lastPopulatedTypeReader.ReadValue_IgnoreProposed(itemToPopulate);
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
                if (lastPopulatedTypeReader?.ReaderType != itemType) lastPopulatedTypeReader = deserializer.GetCachedTypeReader(itemType);
                result = lastPopulatedTypeReader.ReadValue_IgnoreProposed(itemToPopulate);
            }

            return result;
        }
        
    }

    [Flags]
    enum MetaField
    {
        None = 0,
        Id = 1,
        Ref = 2,
        Type = 4,
        Value = 8
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadRefOrProposed<T>(CachedTypeReader typeReader, out T item)
    {
        item = default;
        // ResolveRefs is always false if WriteRefPath is false
        if (!typeReader.WriteRefPath && !typeReader.CheckProposedTypes) return false;

        byte b = SkipWhiteSpaces();
        // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
        // so we can skip the rest of this method and just read it as the original type
        if (b != (byte)'{') return false;
        
        // Peek into buffer to check if we have any meta field (starting with '$') and if not we can leave early.
        if (TryReadRefOrProposed_FindNonMetaField()) return false;

        return TryReadRefOrProposed_Continued(typeReader, out item);
    }

    bool TryReadRefOrProposed_Continued<T>(CachedTypeReader typeReader, out T item)
    {
        item = default;
        using (var undoHandle = CreateUndoReadHandle(true))
        {
            if (!buffer.TryNextByte()) return false;
            if (!TryReadRefOrProposed_TryFindMetaFieldName(
                checkId: typeReader.WriteRefPath,
                checkRef: typeReader.ResolveRefs,
                checkType: typeReader.CheckProposedTypes,
                checkValue: false,
                out MetaField foundMetaField)) return false;

            if (foundMetaField == MetaField.Ref && TryReadRefOrProposed_ResolveRef(out item))
            {
                SetRefInCurrentItemInfo(item);
                SkipRemainingFieldsOfObject();
                undoHandle.SetUndoReading(false);
                return true;
            }

            if (foundMetaField == MetaField.Id)
            {
                TryReadRefOrProposed_HandleId();
                if (SkipWhiteSpaces() == (byte)',') buffer.TryNextByte();
                if (!TryReadRefOrProposed_TryFindMetaFieldName(
                    checkId: false,
                    checkRef: false,
                    checkType: typeReader.CheckProposedTypes,
                    checkValue: true,
                    out foundMetaField)) return false;
            }

            bool readProposedType = false;
            if (foundMetaField == MetaField.Type)
            {
                if (TryReadRefOrProposed_TryReadProposedType(typeReader, typeof(T), out var proposedTypeReader))
                {
                    readProposedType = true;
                    typeReader = proposedTypeReader;
                }

                if (SkipWhiteSpaces() == (byte)',') buffer.TryNextByte();
                // Always check for $value/$values after $type was consumed,
                // even if proposed type was not adopted.
                TryReadRefOrProposed_TryFindMetaFieldName(
                    checkId: false,
                    checkRef: false,
                    checkType: false,
                    checkValue: true,
                    out foundMetaField);
            }

            if (foundMetaField == MetaField.Value)
            {
                item = typeReader.ReadValue_IgnoreProposed<T>();
                SkipRemainingFieldsOfObject();
                undoHandle.SetUndoReading(false);
                return true;
            }
            else if (readProposedType)
            {
                undoHandle.UndoNow();
                item = typeReader.ReadValue_IgnoreProposed<T>();
                undoHandle.SetUndoReading(false);
                return true;
            }
            return false;
        }
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadRefOrProposed<T>(CachedTypeReader typeReader, T itemToPopulate, out T item)
    {
        item = default;
        // ResolveRefs is always false if WriteRefPath is false
        if (!typeReader.WriteRefPath && !typeReader.CheckProposedTypes) return false;

        byte b = SkipWhiteSpaces();
        // If the first non-whitespace character is not a '{', then this can't be an object with a proposed type,
        // so we can skip the rest of this method and just read it as the original type
        if (b != (byte)'{') return false;

        // Peek into buffer to check if we have any meta field (starting with '$') and if not we can leave early.
        if (TryReadRefOrProposed_FindNonMetaField()) return false;

        return TryReadRefOrProposed_Continued(typeReader, itemToPopulate, out item);
    }    

    bool TryReadRefOrProposed_Continued<T>(CachedTypeReader typeReader, T itemToPopulate, out T item)
    {
        item = default;
        using (var undoHandle = CreateUndoReadHandle(true))
        {
            if (!buffer.TryNextByte()) return false;
            if (!TryReadRefOrProposed_TryFindMetaFieldName(
                checkId: typeReader.WriteRefPath,
                checkRef: typeReader.ResolveRefs,
                checkType: typeReader.CheckProposedTypes,
                checkValue: false,
                out MetaField foundMetaField)) return false;

            if (foundMetaField == MetaField.Ref && 
                TryReadRefOrProposed_ResolveRef(out item))
            {
                SetRefInCurrentItemInfo(item);
                SkipRemainingFieldsOfObject();
                undoHandle.SetUndoReading(false);
                return true;
            }

            if (foundMetaField == MetaField.Id)
            {
                TryReadRefOrProposed_HandleId();                
                if (SkipWhiteSpaces() == (byte)',') buffer.TryNextByte();
                if (!TryReadRefOrProposed_TryFindMetaFieldName(
                    checkId: false,
                    checkRef: false,
                    checkType: typeReader.CheckProposedTypes,
                    checkValue: true,
                    out foundMetaField)) return false;
            }

            bool readProposedType = false;
            if (foundMetaField == MetaField.Type)
            {
                if (TryReadRefOrProposed_TryReadProposedType(typeReader, typeof(T), out var proposedTypeReader))
                {
                    readProposedType = true;
                    typeReader = proposedTypeReader;
                }

                if (SkipWhiteSpaces() == (byte)',') buffer.TryNextByte();
                // Always check for $value/$values after $type was consumed,
                // even if proposed type was not adopted.
                TryReadRefOrProposed_TryFindMetaFieldName(
                    checkId: false,
                    checkRef: false,
                    checkType: false,
                    checkValue: true,
                    out foundMetaField);
            }

            // Define a local helper function
            T ReadWithPopulatingIfPossible(CachedTypeReader reader)
            {
                var populateType = itemToPopulate.GetType();
                if (populateType.IsAssignableTo(reader.ReaderType)) return reader.ReadValue_IgnoreProposed<T>(itemToPopulate);
                // If the proposed type is not compatible with the item to populate, we have to read it without populating,
                // otherwise we would populate the wrong type of object
                else return reader.ReadValue_IgnoreProposed<T>();
            }

            if (foundMetaField == MetaField.Value)
            {
                item = ReadWithPopulatingIfPossible(typeReader);
                SkipRemainingFieldsOfObject();
                undoHandle.SetUndoReading(false);
                return true;
            }
            else if (readProposedType)
            {
                undoHandle.UndoNow();
                item = ReadWithPopulatingIfPossible(typeReader);
                undoHandle.SetUndoReading(false);
                return true;
            }
            return false;
        }

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadRefOrProposed_FindNonMetaField()
    {
#if NETSTANDARD2_0
        var peekBytes = buffer.GetRemainingBytes();
#else
        var peekBytes = buffer.GetRemainingSpan();
#endif
        var checkIndex = peekBytes.IndexOf((byte)'"') + 1;
        if (checkIndex <= 0 || checkIndex >= peekBytes.Length) return false;
        return peekBytes[checkIndex] != (byte)'$';
    }

    bool TryReadRefOrProposed_TryReadProposedType(CachedTypeReader originalTypeReader, Type expectedType, out CachedTypeReader proposedTypeReader)
    {
        proposedTypeReader = null;
        var proposedTypeBytes = ReadStringBytes();

        if (originalTypeReader.LastProposedTypeName.IsValid &&
            originalTypeReader.LastProposedTypeName.Equals(proposedTypeBytes) &&
            originalTypeReader.LastProposedTypeReader != null &&
            originalTypeReader.LastProposedTypeReader.ReaderType.IsAssignableTo(expectedType))
        {
            proposedTypeReader = originalTypeReader.LastProposedTypeReader;
            return true;
        }

        proposedTypeBytes.EnsureHashCode();
        // Force a copy of the proposedTypeBytes so it can be safely used as dictionary key without worrying about buffer changes.                
        if (!proposedTypeReaderCache.TryGetValue(proposedTypeBytes, out proposedTypeReader))
        {
            proposedTypeBytes = proposedTypeBytes.CropArray(true);
            proposedTypeReader = null;
            string proposedTypename = Encoding.UTF8.GetString(proposedTypeBytes.AsArraySegment.Array, proposedTypeBytes.AsArraySegment.Offset, proposedTypeBytes.AsArraySegment.Count);
            var proposedType = TypeNameHelper.Shared.GetTypeFromSimplifiedName(proposedTypename);
            if (proposedType == null)
            {
                // Try old format with assembly name for backward compatibility
                try
                {
                    proposedType = Type.GetType(proposedTypename, false, true);
                }
                catch
                {
                    // Ignore any exceptions from Type.GetType and treat it as type not found, to be consistent with TypeNameHelper behavior.
                }
            }

            if (proposedType == null)
            {
                proposedTypeReader = null;
            }
            else
            {
                bool enforceWhitelist = settings.typeWhitelistMode != Settings.TypeWhitelistMode.Disabled;
                if (enforceWhitelist && !IsWhitelistedType(proposedType))
                {
                    if (expectedType.IsInterface || expectedType.IsAbstract)
                    {
                        throw new Exception(
                            $"Proposed type '{proposedTypename}' is not whitelisted and expected type '{expectedType.FullName}' is an interface or abstract class, " +
                            "which is not allowed for security reasons. Consider changing the expected type to a concrete class or adjust the type whitelist settings.");
                    }

                    // No early return here:
                    // proposed type is ignored, fallback to expected type reader later.
                    proposedTypeReader = null;
                }
                else if (proposedType != expectedType && proposedType.IsAssignableTo(expectedType))
                {
                    proposedTypeReader = GetCachedTypeReader(proposedType);
                }
            }

            proposedTypeReaderCache[proposedTypeBytes] = proposedTypeReader;
        }

        bool isProposedTypeCompatible = proposedTypeReader != null && proposedTypeReader.ReaderType.IsAssignableTo(expectedType);
        if (isProposedTypeCompatible)
        {
            originalTypeReader.LastProposedTypeReader = proposedTypeReader;
            originalTypeReader.LastProposedTypeName = proposedTypeBytes;
            return true;
        }
        return false;
    }

    LazyList<ByteSegment> fieldPathSegments = new();
    LazyDictionary<ByteSegment, object> refObjectCache = new();
    static readonly ByteSegment refFieldName = new ByteSegment("$ref".ToByteArray(), true);
    static readonly ByteSegment idFieldName = new ByteSegment("$id".ToByteArray(), true);
    static readonly ByteSegment typeFieldName = new ByteSegment("$type".ToByteArray(), true);
    static readonly ByteSegment valueFieldName = new ByteSegment("$value".ToByteArray(), true);
    static readonly ByteSegment valuesFieldName = new ByteSegment("$values".ToByteArray(), true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadRefOrProposed_TryFindMetaFieldName(bool checkId, bool checkRef, bool checkType, bool checkValue, out MetaField foundMetaField)
    {
        foundMetaField = MetaField.None;
        if (!TryReadStringBytes(out var fieldName)) return false;
        if (checkId && fieldName.Equals(JsonDeserializer.idFieldName)) foundMetaField = MetaField.Id;
        else if (checkRef && fieldName.Equals(JsonDeserializer.refFieldName)) foundMetaField = MetaField.Ref;
        else if (checkType && fieldName.Equals(JsonDeserializer.typeFieldName)) foundMetaField = MetaField.Type;
        else if (checkValue && (fieldName.Equals(JsonDeserializer.valueFieldName) || fieldName.Equals(JsonDeserializer.valuesFieldName))) foundMetaField = MetaField.Value;
        else return false;
        // after the field name, we expect to have a ':' character, so skip it for the next step
        var b = SkipWhiteSpaces();
        if (b != (byte)':') return false;
        buffer.TryNextByte();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void TryReadRefOrProposed_HandleId()
    {
        var idValue = ReadStringBytes();
        if (idValue.Count == 0) throw new InvalidOperationException("Id value cannot be empty.");
        SetIdInCurrentItemInfo(idValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadRefOrProposed_ResolveRef<T>(out T refObject)
    {
        refObject = default;
        var refValue = ReadStringBytes();
        if (refValue.Count == 0) throw new InvalidOperationException("Reference value cannot be empty.");

        // Check cache for refPath to avoid expensive path parsing and itemInfo lookup for already resolved refPaths
        if (refObjectCache.TryGetValue(refValue, out var cachedObjectRef))
        {
            if (cachedObjectRef is T compatibleCachedObjectRef)
            {
                refObject = compatibleCachedObjectRef;
                return true;
            }
            throw new Exception($"Cached reference value '{refValue}' does not point to an object of expected type '{TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(T))}'");
        }

        // If any ref ID was written we first try to resolve the ref value as an ID, because explicit IDs have priority,
        // if that fails and the ref value starts with a '$' we try to resolve it as a refPath, because refPaths always start with a '$'            
        bool foundRef = false;
        if (anyItemIdWritten) foundRef = TryReadRefOrProposed_TryResolveId(refValue, out refObject);
        if (!foundRef && refValue[0] == '$') foundRef = TryReadRefOrProposed_TryResolveRefPath(refValue, out refObject);
        fieldPathSegments.Clear(); // used in TryResolveRefPath, so clear it after each use to avoid stale data in next refPath resolution

        // If a $ref value cannot be resolved, it indicates an invalid JSON structure (e.g. a $ref pointing to a non-existing item or an incorrect refPath), so we throw an exception here
        if (!foundRef) throw new Exception($"Failed to resolve reference value '{refValue}' (path cannot be resolved)");

        // Cache the resolved refObject to avoid repeated resolution attempts for the same refValue.
        refObjectCache[refValue] = refObject;
        return true;
    }

    bool TryReadRefOrProposed_TryResolveRefPath<T>(ByteSegment refPath, out T refObject)
    {
        refObject = default;
        int pos = 0;
        int startPos = 0;
        int segmentLength = 0;
        int refPathCount = refPath.Count;
        var b = refPath.AsArraySegment.Get(pos);

        while (true)
        {
            if (b == '[')
            {
                while (true)
                {
                    pos++;
                    if (pos >= refPathCount) return false;
                    b = refPath.AsArraySegment.Get(pos);
                    if (b == ']')
                    {
                        segmentLength = pos - startPos + 1;
                        pos++;
                        break;
                    }
                }
                ByteSegment segment = refPath.AsArraySegment.Slice(startPos, segmentLength);
                fieldPathSegments.Add(segment);
                if (pos >= refPathCount) break;
                b = refPath.AsArraySegment.Get(pos);
                if (b == '.')
                {
                    pos++;
                    if (pos >= refPathCount) return false;
                }
            }
            else
            {
                while (true)
                {
                    pos++;
                    if (pos >= refPathCount)
                    {
                        segmentLength = pos - startPos;
                        break;
                    }
                    b = refPath.AsArraySegment.Get(pos);
                    if (b == '.')
                    {
                        segmentLength = pos - startPos;
                        pos++;
                        break;
                    }
                    if (b == '[')
                    {
                        segmentLength = pos - startPos;
                        break;
                    }

                }
                ByteSegment segment = refPath.AsArraySegment.Slice(startPos, segmentLength);
                fieldPathSegments.Add(segment);
                if (pos >= refPathCount)
                {
                    if (b == '.' || b == '[') return false;
                    break;
                }
            }
            startPos = pos;
            b = refPath.AsArraySegment.Get(pos);
        }

        // Compare path segments with itemInfos to find the referenced item.
        // Start by comparing the last segment with item names, if it matches,
        // continue comparing parent segments with parent itemInfos until the whole path is validated or invalidated.
        object potentialItemRef = null;
        int lastSegmentIndex = fieldPathSegments.Count - 1;
        var referencedFieldName = fieldPathSegments[lastSegmentIndex];
        bool pathIsValid = false;
        int lastIndex = itemInfos.Count - 1;
        for (int i = lastIndex; i >= 0; i--)
        {
            var info = itemInfos[i];
            if (info.name.Equals(referencedFieldName))
            {
                potentialItemRef = info.itemRef;
                int segmentIndex = lastSegmentIndex - 1;
                int parentIndex = info.parentIndex;
                ItemInfo parentInfo;
                while (segmentIndex != -1 && parentIndex != -1)
                {
                    var segment = fieldPathSegments[segmentIndex];
                    parentInfo = itemInfos[parentIndex];
                    if (!parentInfo.name.Equals(segment)) break;
                    parentIndex = parentInfo.parentIndex;
                    segmentIndex--;
                }

                pathIsValid = parentIndex == -1 && segmentIndex == -1;
                if (pathIsValid)
                {
                    // Cache resolved refPath with itemRef for faster resolution next time,
                    // even if later the type is not compatible in this case
                    refObjectCache[refPath] = potentialItemRef;
                    break;
                }
            }
        }

        if (pathIsValid && potentialItemRef is T compatibleItemRef)
        {
            refObject = compatibleItemRef;
            return true;
        }
        else return false;
    }

    bool TryReadRefOrProposed_TryResolveId<T>(ByteSegment id, out T refObject)
    {
        refObject = default;
        id.EnsureHashCode();

        int lastIndex = itemInfos.Count - 1;
        for (int i = lastIndex; i >= 0; i--)
        {
            var info = itemInfos[i];
            if (info.id != id) continue;
            if (info.itemRef is T compatibleItemRef)
            {
                refObject = compatibleItemRef;
                refObjectCache[id] = refObject;
                return true;
            }
        }

        return false;
    }
}
