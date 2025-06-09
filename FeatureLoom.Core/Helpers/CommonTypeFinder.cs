using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides methods to determine the most specific common type or base type among a set of objects or types.
/// Utilizes caching and thread-safe locking for performance in concurrent scenarios.
/// </summary>
public static class CommonTypeFinder
{
    // Caches results of type comparisons to avoid redundant computation.
    private static readonly Dictionary<(Type, Type), Type> typeCache = new();
    private static FeatureLock cacheLock = new FeatureLock();

    /// <summary>
    /// Determines the most specific common type for a collection of objects.
    /// If all objects are of the same type, returns that type.
    /// If types differ, finds the closest common base type or most derived common interface.
    /// Returns typeof(object) if no commonality is found or the collection is empty.
    /// </summary>
    /// <param name="objects">The collection of objects to analyze.</param>
    /// <returns>The most specific common type, or typeof(object) if none found.</returns>
    public static Type GetCommonType(this IEnumerable objects)
    {
        if (objects.EmptyOrNull())
        {
            return typeof(object);
        }

        var types = objects.Select(obj => obj?.GetType()).Distinct();

        if (types.Skip(1).Any())
        {
            var lockHandle = cacheLock.LockReadOnly();
            try
            {
                var enumerator = types.GetEnumerator();
                if (!enumerator.MoveNext())
                    return typeof(object);

                Type firstType = enumerator.Current;
                bool nullFound = firstType == null;
                bool notNullableTypeFound = firstType != null && !firstType.IsNullable();
                Type commonBaseType = firstType;

                while (enumerator.MoveNext())
                {
                    var type = enumerator.Current;
                    if (type == null)
                    {
                        nullFound = true;
                        if (notNullableTypeFound)
                        {
                            if (commonBaseType == null) commonBaseType = type;
                            commonBaseType = typeof(Nullable<>).MakeGenericType(commonBaseType);
                            notNullableTypeFound = false; // Reset for next iteration
                        }
                        continue;
                    }
                    if (!type.IsNullable()) notNullableTypeFound = true;
                    if (nullFound && notNullableTypeFound)
                    {
                        if (commonBaseType == null) commonBaseType = type;
                        commonBaseType = typeof(Nullable<>).MakeGenericType(commonBaseType);
                        notNullableTypeFound = false; // Reset for next iteration
                    }

                    if (commonBaseType == null)
                    {
                        commonBaseType = type;
                    }
                    else
                    {
                        commonBaseType = GetCommonBaseType(commonBaseType, type, ref lockHandle);
                        if (commonBaseType == null) return typeof(object);
                    }
                }
                if (commonBaseType == null) return typeof(object);
                return commonBaseType;
            }
            finally
            {
                lockHandle.Dispose();
            }
        }
        else
        {
            Type type = types.First();
            if (type == null) type = typeof(object);
            return type;
        }
    }

    /// <summary>
    /// Determines the most specific common base type or most derived common interface between two types.
    /// Returns typeof(object) if no commonality is found.
    /// </summary>
    /// <param name="type1">The first type.</param>
    /// <param name="type2">The second type.</param>
    /// <returns>The most specific common type, or typeof(object) if none found.</returns>
    public static Type GetCommonBaseType(Type type1, Type type2)
    {
        var lockHandle = cacheLock.LockReadOnly();
        try
        {
            Type commonBaseType = GetCommonBaseType(type1, type2, ref lockHandle);
            if (commonBaseType == null) return typeof(object);            
            return commonBaseType;
        }
        finally
        {
            lockHandle.Dispose();
        }
    }

    /// <summary>
    /// Internal method to determine the most specific common base type or most derived common interface between two types.
    /// Uses a cache for performance and is thread-safe.
    /// </summary>
    private static Type GetCommonBaseType(Type type1, Type type2, ref FeatureLock.LockHandle lockHandle)
    {        
        var normalizedKey = NormalizeKey(type1, type2);
        if (typeCache.TryGetValue(normalizedKey, out var cachedType))
        {
            return cachedType;
        }

        // If one type is assignable from the other, return the more general type
        if (type1.IsAssignableFrom(type2))
        {
            lockHandle.UpgradeToWriteMode();
            typeCache[normalizedKey] = type1;
            return type1;
        }

        if (type2.IsAssignableFrom(type1))
        {
            lockHandle.UpgradeToWriteMode();
            typeCache[normalizedKey] = type2;
            return type2;
        }

        // Find the most derived common interface, if any
        var interfaces1 = type1.GetInterfaces();
        var interfaces2 = type2.GetInterfaces();
        var mostDerived = FindMostDerivedCommonInterface(interfaces1, interfaces2);
        if (mostDerived != null)
        {
            lockHandle.UpgradeToWriteMode();
            typeCache[normalizedKey] = mostDerived;
            return mostDerived;
        }

        // Find the closest common base type (excluding object)
        Type baseType1 = type1;
        while (baseType1 != null && baseType1 != typeof(object))
        {
            if (baseType1.IsAssignableFrom(type2))
            {
                lockHandle.UpgradeToWriteMode();
                typeCache[normalizedKey] = baseType1;
                return baseType1;
            }
            baseType1 = baseType1.BaseType;
        }

        // No common base type found, cache the result as null
        lockHandle.UpgradeToWriteMode();
        typeCache[normalizedKey] = null;
        return null;
    }

    /// <summary>
    /// Finds the most derived (i.e., most specific) common interface between two sets of interfaces.
    /// If multiple interfaces are shared, the one that inherits from all others is chosen.
    /// Returns null if there is no common interface.
    /// </summary>
    private static Type FindMostDerivedCommonInterface(Type[] interfaces1, Type[] interfaces2)
    {
        var common = interfaces1.Intersect(interfaces2).ToList();
        if (common.Count == 0) return null;
        // Remove interfaces that are base interfaces of others in the set
        return common
            .Where(i => !common.Any(other => other != i && i.IsAssignableFrom(other)))
            .FirstOrDefault();
    }

    /// <summary>
    /// Normalizes the cache key so that (A, B) and (B, A) are treated the same.
    /// </summary>
    private static (Type, Type) NormalizeKey(Type type1, Type type2)
    {
        return StringComparer.Ordinal.Compare(type1?.FullName, type2?.FullName) <= 0
            ? (type1, type2)
            : (type2, type1);
    }
}
