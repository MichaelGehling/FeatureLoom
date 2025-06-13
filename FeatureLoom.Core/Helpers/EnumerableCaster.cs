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
/// Provides methods to cast <see cref="IEnumerable"/> collections to strongly-typed arrays at runtime,
/// including support for determining and casting to the most specific common type.
/// </summary>
public class EnumerableCaster
{
    // Cache for reflection-created generic TryCastEnumerable methods
    private readonly Dictionary<Type, Func<IEnumerable, IEnumerable>> tryCastEnumerableMethodCache = new Dictionary<Type, Func<IEnumerable, IEnumerable>>();

    // Variables to cache the last used type and its associated delegate
    private Type lastUsedType;
    private Func<IEnumerable, IEnumerable> lastUsedFunc;

    // Shared array for invoking methods via reflection, used to pass the objects
    private readonly object[] parameters = new object[1];

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to a strongly-typed array of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target element type.</typeparam>
    /// <param name="objects">The source collection.</param>
    /// <param name="typedObjects">The resulting strongly-typed array, or null if the cast fails.</param>
    /// <param name="skipCheck">If true, skips type checking for performance (use with caution).</param>
    /// <returns>True if the cast succeeds; otherwise, false.</returns>
    public bool TryCastAllElements<T>(IEnumerable objects, out T[] typedObjects, bool skipCheck = false)
    {
        typedObjects = null;

        Type targetType = typeof(T);
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objects)) return false;

        typedObjects = objects.Cast<T>().ToArray();
        return true;
    }

    // Helper method to cast IEnumerable to IEnumerable<T> (used by compiled delegate)
    private IEnumerable CastEnumerable<T>(IEnumerable objects)
    {
        return objects.Cast<T>().ToArray();
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to an array of the specified element type.
    /// </summary>
    /// <param name="targetType">The target element type.</param>
    /// <param name="objects">The source collection.</param>
    /// <param name="typedObjects">The resulting strongly-typed array, or null if the cast fails.</param>
    /// <param name="skipCheck">If true, skips type checking for performance (use with caution).</param>
    /// <returns>True if the cast succeeds; otherwise, false.</returns>
    public bool TryCastAllElements(Type targetType, IEnumerable objects, out Array typedObjects, bool skipCheck = false)
    {
        typedObjects = null;

        // Check for an empty or null collection
        if (objects.EmptyOrNull()) return false;
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objects)) return false;

        // Check if the last used type is the same as the current target type
        if (targetType == lastUsedType)
        {
            // Invoke the cached delegate
            typedObjects = lastUsedFunc(objects) as Array;
            return typedObjects != null;
        }

        // If not cached, check the dictionary for the delegate
        if (!tryCastEnumerableMethodCache.TryGetValue(targetType, out lastUsedFunc))
        {
            // Create a delegate for the CastEnumerable<T> method
            MethodInfo genericMethod = typeof(EnumerableCaster).GetMethod(nameof(CastEnumerable), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo concreteMethod = genericMethod.MakeGenericMethod(targetType);

            // Create a compiled expression to invoke the method
            var parameter = Expression.Parameter(typeof(IEnumerable), "objects");
            var invokeExpression = Expression.Call(Expression.Constant(this), concreteMethod, parameter);
            lastUsedFunc = Expression.Lambda<Func<IEnumerable, IEnumerable>>(invokeExpression, parameter).Compile();

            // Cache the delegate for future use
            tryCastEnumerableMethodCache[targetType] = lastUsedFunc;
        }

        // Update the last used type
        lastUsedType = targetType;

        // Invoke the cached delegate
        typedObjects = lastUsedFunc(objects) as Array;

        return typedObjects != null;
    }

    /// <summary>
    /// Attempts to cast a collection to an array of its most specific common type.
    /// </summary>
    /// <param name="objects">The source collection.</param>
    /// <param name="commonType">The detected most specific common type for all elements.</param>
    /// <returns>
    /// A strongly-typed array of the common type, but cast to <see cref="Array"/> because its type is only identified at runtime.
    /// </returns>
    public Array CastToCommonTypeArray(IEnumerable objects, out Type commonType)
    {
        commonType = CommonTypeFinder.GetCommonType(objects);
        if (commonType == typeof(object)) return objects.ToArray();
        try
        {
            if (!TryCastAllElements(commonType, objects, out Array typedObjects)) return objects.ToArray();
            return typedObjects;
        }
        catch
        {
            commonType = typeof(object);
            return objects.ToArray();
        }
    }

    /// <summary>
    /// Checks if all elements in the collection are assignable to the target type, considering nullability.
    /// </summary>
    /// <param name="targetType">The target type to check against.</param>
    /// <param name="objects">The collection to check.</param>
    /// <returns>True if all elements are assignable; otherwise, false.</returns>
    private bool CheckIfAllAreAssignable(Type targetType, IEnumerable objects)
    {
        bool targetTypeIsNullable = targetType.IsNullable();
        return objects.All(o => (o == null && targetTypeIsNullable) || (o != null && targetType.IsAssignableFrom(o.GetType())));
    }
}

/// <summary>
/// Provides extension methods for casting <see cref="IEnumerable"/> collections to their most specific common type as arrays.
/// </summary>
public static class EnumerableCasterExtension
{
    static MicroLock myLock = new MicroLock();
    static EnumerableCaster caster = new EnumerableCaster();

    /// <summary>
    /// Casts a collection to an array of its most specific common type, in a thread-safe manner.
    /// </summary>
    /// <param name="objects">The source collection.</param>
    /// <param name="commonType">The detected most specific common type for all elements.</param>
    /// <returns>
    /// A strongly-typed array of the common type, but cast to <see cref="Array"/> because its type is only identified at runtime.
    /// </returns>
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
    /// <typeparam name="T">The target element type.</typeparam>
    /// <param name="objects">The source collection.</param>
    /// <param name="typedObjects">The resulting strongly-typed array, or null if the cast fails.</param>
    /// <param name="skipCheck">If true, skips type checking for performance (use with caution).</param>
    /// <returns>True if the cast succeeds; otherwise, false.</returns>
    public static bool TryCastAllElements<T>(this IEnumerable objects, out T[] typedObjects, bool skipCheck = false)
    {
        using (myLock.Lock())
        {
            return caster.TryCastAllElements<T>(objects, out typedObjects, skipCheck);
        }
    }

    /// <summary>
    /// Attempts to cast an <see cref="IEnumerable"/> to an array of the specified element type in a thread-safe manner.
    /// </summary>
    /// <param name="objects">The source collection.</param>
    /// <param name="targetType">The target element type.</param>
    /// <param name="typedObjects">The resulting strongly-typed array, or null if the cast fails.</param>
    /// <param name="skipCheck">If true, skips type checking for performance (use with caution).</param>
    /// <returns>True if the cast succeeds; otherwise, false.</returns>
    public static bool TryCastAllElements(this IEnumerable objects, Type targetType, out Array typedObjects, bool skipCheck = false)
    {
        using (myLock.Lock())
        {
            return caster.TryCastAllElements(targetType, objects, out typedObjects, skipCheck);
        }
    }
}
