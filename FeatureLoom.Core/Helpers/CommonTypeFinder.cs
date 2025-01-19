using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FeatureLoom.Helpers;

public static class CommonTypeFinder
{
    private static readonly Dictionary<(Type, Type), Type> typeCache = new();
    private static FeatureLock cacheLock = new FeatureLock();

    public static Type GetCommonType(this IEnumerable objects)
    {
        if (objects.EmptyOrNull())
        {
            return typeof(object);
        }

        // Get the types of all objects in the list without creating a new list
        var types = objects.Select(obj => obj?.GetType()).Distinct();

        // If all objects are of the same type
        if (types.Skip(1).Any())
        {            
            var lockHandle = cacheLock.LockReadOnly();
            try
            {
                // If more than one type, proceed to find the common base type
                bool nullFound = false;
                bool notNullableTypeFound = false;
                Type commonBaseType = types.First(); // Get the first type
                foreach (var type in types.Skip(1))
                {
                    if (type == null)
                    {
                        nullFound = true;
                        continue;
                    }
                    if (!type.IsNullable()) notNullableTypeFound |= true;
                    if (nullFound && notNullableTypeFound) return typeof(object);

                    commonBaseType = GetCommonBaseType(commonBaseType, type, ref lockHandle);
                    if (commonBaseType == null) return typeof(object);                    
                }
                return commonBaseType;
            }
            finally
            {
                lockHandle.Dispose();
            }
        }
        else
        {
            // If there is only one type
            Type type = types.First(); // All types are the same
            if (type == null) type = typeof(object);
            return type;
        }
    }

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

    private static Type GetCommonBaseType(Type type1, Type type2, ref FeatureLock.LockHandle lockHandle)
    {        
        // Check if the result is already cached
        if (typeCache.TryGetValue((type1, type2), out var cachedType))
        {
            return cachedType;
        }

        // Check if type1 is assignable from type2 or vice versa
        if (type1.IsAssignableFrom(type2))
        {
            lockHandle.UpgradeToWriteMode();
            typeCache[(type1, type2)] = type1; // Cache the result
            return type1;
        }

        if (type2.IsAssignableFrom(type1))
        {
            lockHandle.UpgradeToWriteMode();
            typeCache[(type1, type2)] = type2; // Cache the result
            return type2;
        }

        // Check for common interfaces
        var interfaces1 = type1.GetInterfaces();
        var interfaces2 = type2.GetInterfaces();

        foreach (var interface1 in interfaces1)
        {
            if (interfaces2.Contains(interface1))
            {
                lockHandle.UpgradeToWriteMode();
                typeCache[(type1, type2)] = interface1; // Cache the result
                return interface1;
            }
        }

        // Find the closest common base type
        Type baseType1 = type1;
        while (baseType1 != null && baseType1 != typeof(object))
        {
            if (baseType1.IsAssignableFrom(type2))
            {
                lockHandle.UpgradeToWriteMode();
                typeCache[(type1, type2)] = baseType1; // Cache the result
                return baseType1;
            }
            baseType1 = baseType1.BaseType;
        }

        // No common base type found, cache the result
        lockHandle.UpgradeToWriteMode();
        typeCache[(type1, type2)] = null;
        return null;
    }
}
