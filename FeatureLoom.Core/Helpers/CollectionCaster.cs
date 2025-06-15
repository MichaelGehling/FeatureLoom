using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides methods to cast <see cref="IEnumerable"/> collections to strongly-typed arrays or lists at runtime,
/// including support for determining and casting to the most specific common type.
/// </summary>
public class CollectionCaster
{
    // Cache for reflection-created generic methods
    private readonly Dictionary<Type, Func<IEnumerable, IEnumerable>> arrayMethodCache = new();
    private readonly Dictionary<Type, Func<IEnumerable, IList>> listMethodCache = new();

    private Type lastArrayType;
    private Func<IEnumerable, IEnumerable> lastArrayFunc;

    private Type lastListType;
    private Func<IEnumerable, IList> lastListFunc;

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed array of <typeparamref name="T"/>.
    /// </summary>
    public bool TryCastAllElementsToArray<T>(IEnumerable objects, out T[] typedArray, bool skipCheck = false)
    {
        typedArray = null;
        Type targetType = typeof(T);
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objects)) return false;
        typedArray = objects.Cast<T>().ToArray();
        return true;
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed array of the specified element type.
    /// </summary>
    public bool TryCastAllElementsToArray(IEnumerable objects, Type targetType, out Array typedArray, bool skipCheck = false)
    {
        typedArray = null;
        if (objects.EmptyOrNull()) return false;
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objects)) return false;

        if (targetType == lastArrayType)
        {
            typedArray = lastArrayFunc(objects) as Array;
            return typedArray != null;
        }

        if (!arrayMethodCache.TryGetValue(targetType, out lastArrayFunc))
        {
            MethodInfo genericMethod = typeof(CollectionCaster).GetMethod(nameof(CastArray), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo concreteMethod = genericMethod.MakeGenericMethod(targetType);
            var parameter = Expression.Parameter(typeof(IEnumerable), "objects");
            var invokeExpression = Expression.Call(Expression.Constant(this), concreteMethod, parameter);
            lastArrayFunc = Expression.Lambda<Func<IEnumerable, IEnumerable>>(invokeExpression, parameter).Compile();
            arrayMethodCache[targetType] = lastArrayFunc;
        }

        lastArrayType = targetType;
        typedArray = lastArrayFunc(objects) as Array;
        return typedArray != null;
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed list of <typeparamref name="T"/>.
    /// </summary>
    public bool TryCastAllElementsToList<T>(IEnumerable objects, out List<T> typedList, bool skipCheck = false)
    {
        typedList = null;
        Type targetType = typeof(T);
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objects)) return false;
        typedList = objects.Cast<T>().ToList();
        return true;
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed list of the specified element type.
    /// </summary>
    public bool TryCastAllElementsToList(IEnumerable objects, Type targetType, out IList typedList, bool skipCheck = false)
    {
        typedList = null;
        if (objects.EmptyOrNull()) return false;
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objects)) return false;

        if (targetType == lastListType)
        {
            typedList = lastListFunc(objects);
            return typedList != null;
        }

        if (!listMethodCache.TryGetValue(targetType, out lastListFunc))
        {
            MethodInfo genericMethod = typeof(CollectionCaster).GetMethod(nameof(CastList), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo concreteMethod = genericMethod.MakeGenericMethod(targetType);
            var parameter = Expression.Parameter(typeof(IEnumerable), "objects");
            var invokeExpression = Expression.Call(Expression.Constant(this), concreteMethod, parameter);
            lastListFunc = Expression.Lambda<Func<IEnumerable, IList>>(invokeExpression, parameter).Compile();
            listMethodCache[targetType] = lastListFunc;
        }

        lastListType = targetType;
        typedList = lastListFunc(objects);
        return typedList != null;
    }

    /// <summary>
    /// Attempts to cast a collection to an array of its most specific common type.
    /// </summary>
    public Array CastToCommonTypeArray(IEnumerable objects, out Type commonType)
    {
        commonType = CommonTypeFinder.GetCommonType(objects);
        if (commonType == typeof(object)) return objects.ToArray();
        try
        {
            if (!TryCastAllElementsToArray(objects, commonType, out Array typedArray)) return objects.ToArray();
            return typedArray;
        }
        catch
        {
            commonType = typeof(object);
            return objects.ToArray();
        }
    }

    /// <summary>
    /// Attempts to cast a collection to a list of its most specific common type.
    /// </summary>
    public IList CastToCommonTypeList(IEnumerable objects, out Type commonType)
    {
        commonType = CommonTypeFinder.GetCommonType(objects);
        if (commonType == typeof(object)) return objects.Cast<object>().ToList();
        try
        {
            if (!TryCastAllElementsToList(objects, commonType, out IList typedList)) return objects.Cast<object>().ToList();
            return typedList;
        }
        catch
        {
            commonType = typeof(object);
            return objects.Cast<object>().ToList();
        }
    }

    // Helper for array casting
    private IEnumerable CastArray<T>(IEnumerable objects) => objects.Cast<T>().ToArray();

    // Helper for list casting
    private IList CastList<T>(IEnumerable objects) => objects.Cast<T>().ToList();

    /// <summary>
    /// Checks if all elements in the collection are assignable to the target type, considering nullability.
    /// </summary>
    private bool CheckIfAllAreAssignable(Type targetType, IEnumerable objects)
    {
        bool targetTypeIsNullable = targetType.IsNullable();
        return objects.All(o => (o == null && targetTypeIsNullable) || (o != null && targetType.IsAssignableFrom(o.GetType())));
    }
}

/// <summary>
/// Provides extension methods for casting <see cref="IEnumerable"/> collections to their most specific common type as arrays or lists.
/// </summary>
public static class CollectionCasterExtension
{
    static MicroLock myLock = new();
    static CollectionCaster caster = new();

    // Array extension methods

    /// <summary>
    /// Casts a collection to an array of its most specific common type, in a thread-safe manner.
    /// </summary>
    public static Array CastToCommonTypeArray(this IEnumerable objects, out Type commonType)
    {
        using (myLock.Lock())
        {
            return caster.CastToCommonTypeArray(objects, out commonType);
        }
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed array of <typeparamref name="T"/> in a thread-safe manner.
    /// </summary>
    public static bool TryCastAllElementsToArray<T>(this IEnumerable objects, out T[] typedArray, bool skipCheck = false)
    {
        using (myLock.Lock())
        {
            return caster.TryCastAllElementsToArray<T>(objects, out typedArray, skipCheck);
        }
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed array of the specified element type in a thread-safe manner.
    /// </summary>
    public static bool TryCastAllElementsToArray(this IEnumerable objects, Type targetType, out Array typedArray, bool skipCheck = false)
    {
        using (myLock.Lock())
        {
            return caster.TryCastAllElementsToArray(objects, targetType, out typedArray, skipCheck);
        }
    }

    // List extension methods

    /// <summary>
    /// Casts a collection to a list of its most specific common type, in a thread-safe manner.
    /// </summary>
    public static IList CastToCommonTypeList(this IEnumerable objects, out Type commonType)
    {
        using (myLock.Lock())
        {
            return caster.CastToCommonTypeList(objects, out commonType);
        }
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed list of <typeparamref name="T"/> in a thread-safe manner.
    /// </summary>
    public static bool TryCastAllElementsToList<T>(this IEnumerable objects, out List<T> typedList, bool skipCheck = false)
    {
        using (myLock.Lock())
        {
            return caster.TryCastAllElementsToList<T>(objects, out typedList, skipCheck);
        }
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed list of the specified element type in a thread-safe manner.
    /// </summary>
    public static bool TryCastAllElementsToList(this IEnumerable objects, Type targetType, out IList typedList, bool skipCheck = false)
    {
        using (myLock.Lock())
        {
            return caster.TryCastAllElementsToList(objects, targetType, out typedList, skipCheck);
        }
    }
}