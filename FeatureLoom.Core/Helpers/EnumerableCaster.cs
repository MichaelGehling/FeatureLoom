using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace FeatureLoom.Helpers;

public class EnumerableCaster
{
    // Cache for reflection-created generic TryCastEnumerable methods
    private readonly Dictionary<Type, Func<IEnumerable, IEnumerable>> tryCastEnumerableMethodCache = new Dictionary<Type, Func<IEnumerable, IEnumerable>>();

    // Variables to cache the last used type and its associated delegate
    private Type lastUsedType;
    private Func<IEnumerable, IEnumerable> lastUsedFunc;

    // Shared array for invoking methods via reflection, used to pass the objects
    private readonly object[] parameters = new object[1];

    // Generic method to cast a IEnumerable to a IEnumerable<T> with an out parameter
    public bool TryCastEnumerable<T>(IEnumerable objects, out IEnumerable<T> typedObjects, bool skipCheck = false)
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

    private bool CheckIfAllAreAssignable(Type targetType, IEnumerable objects)
    {
        bool targetTypeIsNullable = targetType.IsNullable();
        return objects.All(o => (o == null && targetTypeIsNullable) || targetType.IsAssignableFrom(o.GetType()));
    }

    // Non-generic version that takes a Type and casts IEnumerable to an Array of that type using a compiled delegate
    public bool TryCastEnumerable(Type targetType, IEnumerable objects, out IEnumerable typedObjects, bool skipCheck = false)
    {
        typedObjects = null;

        // Check for an empty or null collection
        if (objects.EmptyOrNull()) return false;
        if (!skipCheck && !CheckIfAllAreAssignable(targetType, objects)) return false;

        // Check if the last used type is the same as the current target type
        if (targetType == lastUsedType)
        {
            // Invoke the cached delegate
            typedObjects = lastUsedFunc(objects);
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
        typedObjects = lastUsedFunc(objects);

        return typedObjects != null;
    }

    public IEnumerable ToCommonTypeCollection(IEnumerable objects, out Type commonType)
    {
        commonType = CommonTypeFinder.GetCommonType(objects);
        if (commonType == typeof(object)) return objects;
        try
        {
            if (!TryCastEnumerable(commonType, objects, out IEnumerable typedObjects)) return objects;
            return typedObjects;
        }
        catch
        {
            return objects;
        }
    }

}

public static class EnumerableCasterExtension
{
    static MicroLock myLock = new MicroLock();
    static EnumerableCaster caster = new EnumerableCaster();
    public static IEnumerable CastToCommonTypeCollection(this IEnumerable objects, out Type commonType)
    {
        using (myLock.Lock())
        {
            return caster.ToCommonTypeCollection(objects, out commonType);
        }
    }
}
