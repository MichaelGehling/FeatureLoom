using FeatureLoom.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace FeatureLoom.Helpers;


public class ListCaster
{
    // Cache for reflection-created generic TryCastList methods
    private readonly Dictionary<Type, Func<List<object>, IList>> tryCastListMethodCache = new Dictionary<Type, Func<List<object>, IList>>();

    // Variables to cache the last used type and its associated delegate
    private Type lastUsedType;
    private Func<List<object>, IList> lastUsedFunc;

    // Shared array for invoking methods via reflection, used to pass the objectList
    private readonly object[] parameters = new object[1];

    // Generic method to cast a List<object> to a List<T> with an out parameter
    public bool TryCastList<T>(List<object> objectList, out List<T> typedList, bool skipCheck = false)
    {
        typedList = null;

        Type targetType = typeof(T);
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objectList)) return false;

        typedList = objectList.Cast<T>().ToList();
        return true;
        
    }

    // Helper method to cast List<object> to List<T> (used by compiled delegate)
    private List<T> CastList<T>(List<object> objectList)
    {
        return objectList.Cast<T>().ToList();
    }

    private bool CheckIfAllAreAssignable(Type targetType, List<object> objectList)
    {
        bool targetTypeIsNullable = targetType.IsNullable();
        return objectList.All(o => (o == null && targetTypeIsNullable) || targetType.IsAssignableFrom(o.GetType()));
    }

    // Non-generic version that takes a Type and casts List<object> to a List of that type using a compiled delegate
    public bool TryCastList(Type targetType, List<object> objectList, out IList typedList, bool skipCheck = false)
    {
        typedList = null;

        // Check for an empty or null list
        if (objectList == null || objectList.Count == 0) return false;
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objectList)) return false;

        // Check if the last used type is the same as the current target type
        if (targetType == lastUsedType)
        {
            // Invoke the cached delegate
            typedList = lastUsedFunc(objectList);
            return typedList != null;
        }

        // If not cached, check the dictionary for the delegate
        if (!tryCastListMethodCache.TryGetValue(targetType, out lastUsedFunc))
        {
            // Create a delegate for the CastList<T> method
            MethodInfo genericMethod = typeof(ListCaster).GetMethod(nameof(CastList), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo concreteMethod = genericMethod.MakeGenericMethod(targetType);

            // Create a compiled expression to invoke the method
            var parameter = Expression.Parameter(typeof(List<object>), "objectList");
            var invokeExpression = Expression.Call(Expression.Constant(this), concreteMethod, parameter);
            lastUsedFunc = Expression.Lambda<Func<List<object>, IList>>(invokeExpression, parameter).Compile();

            // Cache the delegate for future use
            tryCastListMethodCache[targetType] = lastUsedFunc;
        }

        // Update the last used type
        lastUsedType = targetType;

        // Invoke the cached delegate
        typedList = lastUsedFunc(objectList);

        return typedList != null;
    }

    public IList CastListToCommonType(List<object> objectList, out Type commonType)
    {
        commonType =CommonTypeFinder.GetCommonType(objectList);
        if (commonType == typeof(object)) return objectList;
        try
        {
            if (!TryCastList(commonType, objectList, out IList typedList)) return objectList;
            return typedList;
        }
        catch
        {
            return objectList;
        }
    }
}