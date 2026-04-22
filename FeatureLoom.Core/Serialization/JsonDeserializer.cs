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
    bool isPopulating = false;

    struct ItemInfo
    {
        public readonly ByteSegment name;            
        public readonly int parentIndex;
        public object itemRef;

        public ItemInfo(ByteSegment name, int parentIndex)
        {
            this.name = name;
            this.parentIndex = parentIndex;
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

    public JsonDeserializer(Action<Settings> buildSettings) : this(Settings.Build(buildSettings))
    {

    }

    public JsonDeserializer(Settings deserializerSettings = null)
    {
        deserializerSettings = deserializerSettings ?? new Settings();
        this.settings = new CompiledSettings(deserializerSettings);            
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
            stringCache = new QuickStringCache(settings.stringCacheBitSize, settings.stringCacheMaxLength);
        }
    }

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

        if (settings.referenceResolutionMode != Settings.ReferenceResolutionMode.ForceDisabled)
        {
            currentItemName = rootName;                
            itemInfos.Clear();
            currentItemInfoIndex = -1;
        }
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

    private bool TryDeserializeLocked<T>(out T item)
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

                var itemType = typeof(T);
                if (lastTypeReader?.ReaderType == itemType)
                {
                    item = lastTypeReader.ReadFieldValue<T>(rootName);
                }
                else
                {
                    var reader = GetCachedTypeReader(itemType);
                    lastTypeReader = reader;
                    item = reader.ReadFieldValue<T>(rootName);
                }
                return true;
            }
            catch (BufferExceededException)
            {

                buffer.ResetAfterBufferExceededException();

                currentItemName = rootName;                    
                itemInfos.Clear();
                currentItemInfoIndex = -1;

                if (!buffer.TryReadFromStream() && !IsAnyDataLeftUnlocked())
                {
                    item = default;
                    return false;
                }

                retry = true;
            }
            catch (Exception e)
            {
                if (settings.logCatchedExceptions) OptLog.ERROR()?.Build($"Exception occurred on deserialation at buffer position {buffer.BufferPos}. SampleFromBuffer(50 chars before and after): {buffer.ShowBufferAroundCurrentPosition(50, 50)}", e);
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

                if (lastTypeReader?.ReaderType == itemType)
                {
                    item = lastTypeReader.ReadFieldValue<object>(rootName);
                }
                else
                {
                    var reader = GetCachedTypeReader(itemType);
                    lastTypeReader = reader;
                    item = reader.ReadFieldValue<object>(rootName);
                }
                return true;
            }
            catch (BufferExceededException)
            {

                buffer.ResetAfterBufferExceededException();

                currentItemName = rootName;
                itemInfos.Clear();
                currentItemInfoIndex = -1;

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
                if (lastTypeReader?.ReaderType == itemType)
                {
                    item = lastTypeReader.ReadFieldValue(rootName, item);
                }
                else
                {
                    var reader = GetCachedTypeReader(itemType);
                    lastTypeReader = reader;
                    item = reader.ReadFieldValue(rootName, item);
                }
                return true;
            }
            catch (BufferExceededException)
            {

                buffer.ResetAfterBufferExceededException();

                currentItemName = rootName;
                itemInfos.Clear();
                currentItemInfoIndex = -1;

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
