using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;
using static FeatureLoom.Serialization.JsonSerializer;

#if !NETSTANDARD2_0
using System.Buffers.Text;
using System.Buffers;
#endif

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{        
    readonly Buffer buffer = new Buffer();

    MicroValueLock serializerLock = new MicroValueLock();                        
    
    static readonly ByteSegment rootName = new ByteSegment("$".ToByteArray(), true);
    ByteSegment currentItemName = rootName;
    int currentItemInfoIndex = -1;
    List<ItemInfo> itemInfos = new List<ItemInfo>();
    bool anyItemIdWritten = false;
    bool isPopulating = false;

    struct ItemInfo
    {
        public readonly ByteSegment name;            
        public readonly int parentIndex;
        public object itemRef;
        public ByteSegment id;

        public ItemInfo(ByteSegment name, int parentIndex)
        {
            this.name = name;
            this.parentIndex = parentIndex;
        }

        public ItemInfo(ByteSegment id)
        {            
            this.parentIndex = -1;
            this.id = id;
        }
    }

    static ulong[] CreateExponentFactorMap(int maxExponent)
    {
        ulong[] map = new ulong[maxExponent + 1];
        ulong factor = 1;
        map[0] = factor;
        for (int i = 1; i < map.Length; i++)
        {
            factor *= 10;
            map[i] = factor;
        }
        return map;
    }

    public enum DataAccess
    {
        PublicAndPrivateFields = 0,
        PublicFieldsAndProperties = 1
    }

    readonly CompiledSettings settings;
    // Cached once, because it is checked on every single deserialization. Reading it from the
    // settings object each time is a dependent load on the entry path.
    readonly bool refResolutionEnabled;

    public JsonDeserializer(Action<Settings> buildSettings) : this(Settings.Build(buildSettings))
    {

    }

    public JsonDeserializer(Settings deserializerSettings = null)
    {
        deserializerSettings = deserializerSettings ?? new Settings();
        this.settings = new CompiledSettings(deserializerSettings);            
        this.refResolutionEnabled = settings.referenceResolutionMode != Settings.ReferenceResolutionMode.ForceDisabled;
        buffer.Init(settings.initialBufferSize);            
        preparationApi = new PreparationApi(this);
        extensionApi = new ExtensionApi(this);
        isPopulating = settings.populateExistingMembers;
        useStringCache = settings.useStringCache;

        if (settings.anyAllowsProposedTypes)
        {
            foreach (var kvp in settings.customTypeNames)
            {
                if (kvp.Key.EmptyOrNull()) continue;
                var cachedTypeReader = GetCachedTypeReader(kvp.Value);
                AddCustomTypeNameToProposedCache(kvp.Key, cachedTypeReader, settings.addCaseVariantsForCustomTypeNames);
            }
        }

        if (settings.anyUsesStringCache)
        {
            stringCache = new Utf8StringCache(settings.stringCacheBitSize, settings.stringCacheMaxLength);
        }
    }

    /// <summary>
    /// Number of string-cache lookups that were resolved from an existing entry, avoiding a new
    /// string allocation. Zero when string caching is not used.
    /// </summary>
    public long StringCacheHitCount => stringCache?.HitCount ?? 0;

    /// <summary>
    /// Number of string-cache lookups that had to decode and store a new string.
    /// Zero when string caching is not used.
    /// </summary>
    public long StringCacheMissCount => stringCache?.MissCount ?? 0;

    /// <summary>
    /// Ratio of string-cache hits to total lookups in the range [0..1].
    /// <para>
    /// This is the main indicator for tuning string-cache usage: a low ratio means the cached
    /// members mostly carry unique values, paying hashing, probing and eviction cost without
    /// saving allocations. Those members can be excluded individually via
    /// <c>ConfigureMember(..., ms =&gt; ms.SetUseStringCache(false))</c>.
    /// </para>
    /// </summary>
    public double StringCacheHitRatio => stringCache?.HitRatio ?? 0d;

    /// <summary>
    /// Resets the string-cache hit/miss statistics without dropping the cached strings.
    /// </summary>
    public void ResetStringCacheStatistics() => stringCache?.ResetStatistics();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddCustomTypeNameToProposedCache(string customTypeName, CachedTypeReader cachedTypeReader, bool addCaseVariants)
    {
        proposedTypeReaderCache[new ByteSegment(customTypeName, true)] = cachedTypeReader;

        if (!addCaseVariants) return;

        string lower = customTypeName.ToLowerInvariant();
        if (lower != customTypeName)
        {
            proposedTypeReaderCache[new ByteSegment(lower, true)] = cachedTypeReader;
        }

        string upper = customTypeName.ToUpperInvariant();
        if (upper != customTypeName && upper != lower)
        {
            proposedTypeReaderCache[new ByteSegment(upper, true)] = cachedTypeReader;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reset()
    {
        buffer.ResetAfterReading();
        isPopulating = settings.populateExistingMembers;
        ResetRefResolutionHelper();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetRefResolutionHelper()
    {
        if (!refResolutionEnabled) return;

        currentItemName = rootName;
        currentItemInfoIndex = -1;
        anyItemIdWritten = false;
        // Clearing an already empty collection is the common case on the entry path, so the
        // non-inlined Clear() calls are skipped unless something was actually recorded.
        if (itemInfos.Count > 0) itemInfos.Clear();
        if (refObjectCache.Count > 0) refObjectCache.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    CachedTypeReader GetCachedTypeReader(Type itemType)
    {
        if (typeReaderCache.TryGetValue(itemType, out var cachedTypeReader)) return cachedTypeReader;
        else return CreateCachedTypeReader(itemType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    CachedTypeReader GetCachedTypeReader(Type itemType, BaseTypeSettings typeSettings)
    {
        if (typeSettings == null && typeReaderCache.TryGetValue(itemType, out var cachedTypeReader)) return cachedTypeReader;
        else return CreateCachedTypeReader(itemType, typeSettings);
    }

    CachedTypeReader lastTypeReader = null;
    Type lastTypeReaderType = null;

    /// <summary>
    /// Fast path of the generic deserialization. It is kept free of a retry loop and of a
    /// finally-funclet, because both add fixed cost to every single call. Reset() is invoked
    /// explicitly on each exit instead, and the rare recovery/retry handling is delegated to
    /// the cold <see cref="TryDeserializeLockedAfterBufferExceeded{T}(out T)"/>.
    /// </summary>
    private bool TryDeserializeLocked<T>(out T item)
    {
        try
        {
            if (!buffer.TryPrepareDeserialization())
            {
                item = default;
                Reset();
                return false;
            }

            // Return false if only whitespaces are left (otherwise we would throw an exception)
            byte b = SkipWhiteSpaces();
            if (IsWhiteSpace(b))
            {
                item = default;
                Reset();
                return false;
            }

            var itemType = typeof(T);
            if (lastTypeReaderType == itemType)
            {
                item = lastTypeReader.ReadFieldValue<T>(rootName);
            }
            else
            {
                var reader = GetCachedTypeReader(itemType);
                lastTypeReader = reader;
                lastTypeReaderType = itemType;
                item = reader.ReadFieldValue<T>(rootName);
            }
            Reset();
            return true;
        }
        catch (BufferExceededException)
        {
            return TryDeserializeLockedAfterBufferExceeded(out item);
        }
        catch (Exception e)
        {
            if (settings.logCatchedExceptions) OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
            Reset();
            if (settings.rethrowExceptions) throw;
            item = default;
            return false;
        }
    }

    /// <summary>
    /// Cold continuation of <see cref="TryDeserializeLocked{T}(out T)"/> that refills the buffer
    /// and retries after a <see cref="BufferExceededException"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryDeserializeLockedAfterBufferExceeded<T>(out T item)
    {
        while (true)
        {
            buffer.ResetAfterBufferExceededException();

            ResetRefResolutionHelper();

            if (!buffer.TryReadFromStream() && !IsAnyDataLeftUnlocked())
            {
                item = default;
                Reset();
                return false;
            }

            try
            {
                if (!buffer.TryPrepareDeserialization())
                {
                    item = default;
                    Reset();
                    return false;
                }

                // Return false if only whitespaces are left (otherwise we would throw an exception)
                byte b = SkipWhiteSpaces();
                if (IsWhiteSpace(b))
                {
                    item = default;
                    Reset();
                    return false;
                }

                var itemType = typeof(T);
                if (lastTypeReaderType == itemType)
                {
                    item = lastTypeReader.ReadFieldValue<T>(rootName);
                }
                else
                {
                    var reader = GetCachedTypeReader(itemType);
                    lastTypeReader = reader;
                    lastTypeReaderType = itemType;
                    item = reader.ReadFieldValue<T>(rootName);
                }
                Reset();
                return true;
            }
            catch (BufferExceededException)
            {
                continue;
            }
            catch (Exception e)
            {
                if (settings.logCatchedExceptions) OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
                Reset();
                if (settings.rethrowExceptions) throw;
                item = default;
                return false;
            }
        }
    }

    private bool TryDeserializeLocked(Type itemType, out object item)
    {
        item = default;
        bool retry = false;
        do
        {
            retry = false;
            try
            {
                if (!buffer.TryPrepareDeserialization())
                {
                    item = default;
                    return false;
                }

                // Return false if only whitespaces are left (otherwise we would throw an exception)
                byte b = SkipWhiteSpaces();
                if (IsWhiteSpace(b)) return false;

                if (lastTypeReaderType == itemType)
                {
                    item = lastTypeReader.ReadFieldValue<object>(rootName);
                }
                else
                {
                    var reader = GetCachedTypeReader(itemType);
                    lastTypeReader = reader;
                    lastTypeReaderType = itemType;
                    item = reader.ReadFieldValue<object>(rootName);
                }
                return true;
            }
            catch (BufferExceededException)
            {

                buffer.ResetAfterBufferExceededException();

                ResetRefResolutionHelper();

                if (!buffer.TryReadFromStream() && !IsAnyDataLeftUnlocked())
                {
                    item = default;
                    return false;
                }

                retry = true;
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
                if (settings.rethrowExceptions) throw;
            }
            finally
            {
                if (!retry)
                {
                    Reset();
                }
            }
        } while (retry);

        return false;
    }

    private bool TryPopulateLocked<T>(ref T item)
    {
        bool retry = false;
        do
        {
            isPopulating = true;
            retry = false;
            try
            {
                if (!buffer.TryPrepareDeserialization())
                {                        
                    return false;
                }

                // Return false if only whitespaces are left (otherwise we would throw an exception)
                byte b = SkipWhiteSpaces();
                if (IsWhiteSpace(b)) return false;
                
                var itemType = item != null ? item.GetType() : typeof(T);
                if (lastTypeReaderType == itemType)
                {
                    item = lastTypeReader.ReadFieldValue(rootName, item);
                }
                else
                {
                    var reader = GetCachedTypeReader(itemType);
                    lastTypeReader = reader;
                    lastTypeReaderType = itemType;
                    item = reader.ReadFieldValue(rootName, item);
                }
                return true;
            }
            catch (BufferExceededException)
            {

                buffer.ResetAfterBufferExceededException();

                ResetRefResolutionHelper();

                if (!buffer.TryReadFromStream() && !IsAnyDataLeftUnlocked())
                {
                    // At this point the item is probably partially populated
                    return false;
                }

                retry = true;
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
                if (settings.rethrowExceptions) throw;
            }
            finally
            {
                if (!retry)
                {
                    Reset();
                }
            }
        } while (retry);

        return false;
    }
    public bool TryDeserialize<T>(out T item)
    {
        serializerLock.Enter();
        try
        {
            return TryDeserializeLocked(out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize(Type type, out object item)
    {
        serializerLock.Enter();
        try
        {
            return TryDeserializeLocked(type, out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize<T>(Stream stream, out T item)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(stream);
            return TryDeserializeLocked(out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize(Stream stream, Type type, out object item)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(stream);
            return TryDeserializeLocked(type, out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize<T>(string json , out T item)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(json);
            return TryDeserializeLocked(out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize(string json, Type type, out object item)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(json);
            return TryDeserializeLocked(type, out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize<T>(ByteSegment utf8Bytes, out T item)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(utf8Bytes);
            return TryDeserializeLocked(out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize(ByteSegment utf8Bytes, Type type, out object item)
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(utf8Bytes);
            return TryDeserializeLocked(type, out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize<T>(byte[] utf8Bytes, out T item) => TryDeserialize(new ByteSegment(utf8Bytes, true), out item);
     
    public bool TryDeserialize(byte[] utf8Bytes, Type type, out object item) => TryDeserialize(new ByteSegment(utf8Bytes, true), type, out item);

    public bool TryDeserialize<T>(JsonFragment json, out T item)
    {
        serializerLock.Enter();
        try
        {
            if (json.IsString) SetDataSourceUnlocked(json.JsonString);
            else if (json.IsUtf8) SetDataSourceUnlocked(json.JsonUtf8);
            else
            {
                item = default;
                return false;
            }
            return TryDeserializeLocked(out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryDeserialize(JsonFragment json, Type type, out object item)
    {
        serializerLock.Enter();
        try
        {
            if (json.IsString) SetDataSourceUnlocked(json.JsonString);
            else if (json.IsUtf8) SetDataSourceUnlocked(json.JsonUtf8);
            else
            {
                item = default;
                return false;
            }
            return TryDeserializeLocked(type, out item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }


    public bool TryPopulate<T>(ref T item) where T : struct
    {
        serializerLock.Enter();
        try
        {
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(Stream stream, ref T item) where T : struct
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(stream);
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(string json, ref T item) where T : struct
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(json);
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(JsonFragment json, ref T item) where T : struct
    {
        serializerLock.Enter();
        try
        {
            if (json.IsString) SetDataSourceUnlocked(json.JsonString);
            else if (json.IsUtf8) SetDataSourceUnlocked(json.JsonUtf8);
            else
            {
                item = default;
                return false;
            }
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(ByteSegment utf8Bytes, ref T item) where T : struct
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(utf8Bytes);
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(T item) where T : class
    {
        serializerLock.Enter();
        try
        {
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(Stream stream, T item) where T : class
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(stream);
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(string json, T item) where T : class
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(json);
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }

    public bool TryPopulate<T>(ByteSegment utf8Bytes, T item) where T : class
    {
        serializerLock.Enter();
        try
        {
            SetDataSourceUnlocked(utf8Bytes);
            return TryPopulateLocked(ref item);
        }
        finally
        {
            serializerLock.Exit();
        }
    }
    
}
