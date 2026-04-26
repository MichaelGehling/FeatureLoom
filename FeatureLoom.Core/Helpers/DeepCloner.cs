using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides deep cloning for arbitrary object graphs.
/// Supports primitives, value types, classes, arrays, collections, dictionaries,
/// shared references, and cyclic references.
/// </summary>
public static class DeepCloner
{
    public enum DelegateCloneHandling
    {
        /// <summary>
        /// Delegates are copied by reference.
        /// </summary>
        CopyReference = 0,

        /// <summary>
        /// After cloning, delegate targets are rebound only when the original target is part of the cloned object graph.
        /// Targets outside the cloned graph remain unchanged.
        /// </summary>
        RebindKnownTargetsAfterClone = 1,

        /// <summary>
        /// WARNING: Rebinds delegate targets even when they were not part of the original cloned graph.
        /// This can clone additional external object graphs, change identity semantics, and increase runtime/memory cost.
        /// </summary>
        RebindAllTargetsAfterClone = 2
    }

    public enum TaskCloneHandling
    {
        /// <summary>
        /// Tasks are copied by reference.
        /// </summary>
        CopyReference = 0,

        /// <summary>
        /// For successfully completed <c>Task&lt;TResult&gt;</c>, creates a new completed task with a deep-cloned result.
        /// All other tasks are copied by reference.
        /// </summary>
        CloneCompletedResult = 1
    }

    /// <summary>
    /// Configures deep-clone behavior.
    /// </summary>
    public readonly struct Settings
    {
        /// <summary>
        /// Gets the default settings used by <see cref="TryClone{T}(T, out T)"/>.
        /// </summary>
        /// <remarks>
        /// Defaults are:
        /// <list type="bullet">
        /// <item><description><see cref="DelegateCloneHandling.RebindKnownTargetsAfterClone"/></description></item>
        /// <item><description><see cref="TaskCloneHandling.CopyReference"/></description></item>
        /// </list>
        /// </remarks>
        public static Settings Default => new Settings(
            DelegateCloneHandling.RebindKnownTargetsAfterClone,
            TaskCloneHandling.CopyReference);

        /// <summary>
        /// Initializes settings with explicit delegate and task handling.
        /// </summary>
        public Settings(DelegateCloneHandling delegateHandling, TaskCloneHandling taskHandling)
        {
            DelegateHandling = delegateHandling;
            TaskHandling = taskHandling;
        }

        /// <summary>
        /// Gets delegate clone handling.
        /// </summary>
        public DelegateCloneHandling DelegateHandling { get; }

        /// <summary>
        /// Gets task clone handling.
        /// </summary>
        public TaskCloneHandling TaskHandling { get; }

        /// <summary>
        /// Returns a copy of these settings with a different delegate handling mode.
        /// </summary>
        public Settings WithDelegateHandling(DelegateCloneHandling value) => new Settings(value, TaskHandling);

        /// <summary>
        /// Returns a copy of these settings with a different task handling mode.
        /// </summary>
        public Settings WithTaskHandling(TaskCloneHandling value) => new Settings(DelegateHandling, value);
    }

    /// <summary>
    /// Attempts to create a deep clone of <paramref name="obj"/> using <see cref="Settings.Default"/>.
    /// </summary>
    /// <remarks>
    /// Default behavior:
    /// <list type="bullet">
    /// <item><description>Delegates: <see cref="DelegateCloneHandling.RebindKnownTargetsAfterClone"/></description></item>
    /// <item><description>Tasks: <see cref="TaskCloneHandling.CopyReference"/></description></item>
    /// </list>
    /// </remarks>
    public static bool TryClone<T>(T obj, out T clone)
    {
        return TryClone(obj, out clone, Settings.Default);
    }

    /// <summary>
    /// Attempts to create a deep clone of <paramref name="obj"/> using explicit <paramref name="settings"/>.
    /// </summary>
    public static bool TryClone<T>(T obj, out T clone, in Settings settings)
    {
        var visited = visitedPool.Take();
        try
        {
            bool rebindDelegates =
                settings.DelegateHandling == DelegateCloneHandling.RebindKnownTargetsAfterClone ||
                settings.DelegateHandling == DelegateCloneHandling.RebindAllTargetsAfterClone;

            bool rebindAllTargets = settings.DelegateHandling == DelegateCloneHandling.RebindAllTargetsAfterClone;

            if (rebindDelegates)
            {
                visited[DelegateRebindEnabledKey] = DelegateRebindEnabledKey;
            }

            // Keep this for task handling logic (already in settings-based API).
            visited[TaskCloneHandlingKey] = settings.TaskHandling;

            if (!TryCloneInternal(obj, out clone, visited))
            {
                return false;
            }

            if (rebindDelegates)
            {
                if (!TryFinalizeDelegateRebinding(visited, rebindAllTargets))
                {
                    clone = default;
                    return false;
                }

                if (clone is Delegate cloneDelegate &&
                    TryRebindDelegate(cloneDelegate, visited, rebindAllTargets, out var reboundRootDelegate))
                {
                    clone = (T)(object)reboundRootDelegate;
                }
            }

            return true;
        }
        finally
        {
            if (visited.TryGetValue(DelegateRebindWorklistKey, out var worklistObj))
            {
                delegateRebindWorklistPool.Return((List<DelegateRebindEntry>)worklistObj);
            }

            visitedPool.Return(visited);
        }
    }

    /// <summary>
    /// Attempts to create a deep clone of <paramref name="obj"/> by transforming <see cref="Settings.Default"/>.
    /// </summary>
    /// <param name="configureSettings">
    /// Function that receives <see cref="Settings.Default"/> and returns the settings to use.
    /// If <see langword="null"/>, <see cref="Settings.Default"/> is used unchanged.
    /// </param>
    public static bool TryClone<T>(T obj, out T clone, Func<Settings, Settings> configureSettings)
    {
        var settings = configureSettings == null ? Settings.Default : configureSettings(Settings.Default);
        return TryClone(obj, out clone, in settings);
    }

    /// <summary>
    /// Backward-compatible overload that overrides only delegate handling while keeping default task handling.
    /// </summary>
    /// <remarks>
    /// Equivalent to:
    /// <c>TryClone(obj, out clone, s =&gt; s.WithDelegateHandling(delegateHandling))</c>.
    /// </remarks>
    public static bool TryClone<T>(T obj, out T clone, DelegateCloneHandling delegateHandling)
    {
        var settings = Settings.Default.WithDelegateHandling(delegateHandling);
        return TryClone(obj, out clone, in settings);
    }

    /// <summary>
    /// Backward-compatible overload with explicit delegate and task handling.
    /// </summary>
    /// <remarks>
    /// Equivalent to:
    /// <c>TryClone(obj, out clone, new Settings(delegateHandling, taskHandling))</c>.
    /// </remarks>
    public static bool TryClone<T>(T obj, out T clone, DelegateCloneHandling delegateHandling, TaskCloneHandling taskHandling)
    {
        var visited = visitedPool.Take();
        try
        {
            bool rebindDelegates =
                delegateHandling == DelegateCloneHandling.RebindKnownTargetsAfterClone ||
                delegateHandling == DelegateCloneHandling.RebindAllTargetsAfterClone;

            bool rebindAllDelegateTargets = delegateHandling == DelegateCloneHandling.RebindAllTargetsAfterClone;

            if (rebindDelegates)
            {
                visited[DelegateRebindEnabledKey] = DelegateRebindEnabledKey;
            }

            if (taskHandling != TaskCloneHandling.CopyReference)
            {
                visited[TaskCloneHandlingKey] = taskHandling;
            }

            if (!TryCloneInternal(obj, out clone, visited))
            {
                return false;
            }

            if (rebindDelegates)
            {
                if (!TryFinalizeDelegateRebinding(visited, rebindAllDelegateTargets))
                {
                    clone = default;
                    return false;
                }

                if (clone is Delegate cloneDelegate &&
                    TryRebindDelegate(cloneDelegate, visited, rebindAllDelegateTargets, out var reboundRootDelegate))
                {
                    clone = (T)(object)reboundRootDelegate;
                }
            }

            return true;
        }
        finally
        {
            if (visited.TryGetValue(DelegateRebindWorklistKey, out var worklistObj))
            {
                delegateRebindWorklistPool.Return((List<DelegateRebindEntry>)worklistObj);
            }

            visitedPool.Return(visited);
        }
    }

    /// <summary>
    /// Type-specific clone entry point routed through cached clone delegates.
    /// </summary>
    private static bool TryCloneInternal<T>(T obj, out T clone, IDictionary<object, object> visited)
    {
        if (obj is null)
        {
            clone = default;
            return true;
        }

        var declaredType = typeof(T);
        if (!declaredType.IsValueType)
        {
            object sourceObj = obj;
            var runtimeType = sourceObj.GetType();

            if (runtimeType != declaredType)
            {
                if (!TryCloneInternal(sourceObj, out object runtimeClone, visited))
                {
                    clone = default;
                    return false;
                }

                clone = (T)runtimeClone;
                return true;
            }
        }

        return ClonerCache<T>.TryClone(obj, visited, out clone);
    }

    /// <summary>
    /// Runtime-type clone entry point used when only <see cref="object"/> is known.
    /// </summary>
    private static bool TryCloneInternal(object obj, out object clone, IDictionary<object, object> visited)
    {
        if (obj == null)
        {
            clone = null;
            return true;
        }

        var runtimeType = obj.GetType();
        var runtimeCloner = RuntimeClonerCache.GetOrAdd(runtimeType, BuildRuntimeCloner);
        return runtimeCloner(obj, visited, out clone);
    }

    /// <summary>
    /// Builds and caches a runtime bridge delegate for a concrete runtime type.
    /// </summary>
    private static RuntimeClonerFunction BuildRuntimeCloner(Type runtimeType)
    {
        var method = typeof(DeepCloner)
            .GetMethod(nameof(TryCloneRuntimeBridge), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(runtimeType);

        return (RuntimeClonerFunction)Delegate.CreateDelegate(typeof(RuntimeClonerFunction), method);
    }

    /// <summary>
    /// Generic bridge for runtime-dispatched cloning.
    /// </summary>
    private static bool TryCloneRuntimeBridge<TRuntime>(object source, IDictionary<object, object> visited, out object clone)
    {
        if (!TryCloneInternal((TRuntime)source, out TRuntime typedClone, visited))
        {
            clone = null;
            return false;
        }

        clone = typedClone;
        return true;
    }

    /// <summary>
    /// Clones arrays of any rank including non-zero lower bounds.
    /// </summary>
    private static bool TryCloneArray(Array sourceArray, out Array clonedArray, IDictionary<object, object> visited)
    {
        if (visited.TryGetValue(sourceArray, out object existingArray))
        {
            clonedArray = (Array)existingArray;
            return true;
        }

        if (sourceArray.Rank == 1 && sourceArray.GetLowerBound(0) == 0)
        {
            var szArrayCloner = SzArrayClonerCache.GetOrAdd(sourceArray.GetType(), BuildSzArrayCloner);
            return szArrayCloner(sourceArray, visited, out clonedArray);
        }

        Type elementType = sourceArray.GetType().GetElementType();
        int rank = sourceArray.Rank;

        var lengths = new int[rank];
        var lowerBounds = new int[rank];
        for (int i = 0; i < rank; i++)
        {
            lengths[i] = sourceArray.GetLength(i);
            lowerBounds[i] = sourceArray.GetLowerBound(i);
        }

        clonedArray = Array.CreateInstance(elementType, lengths, lowerBounds);
        visited[sourceArray] = clonedArray;

        if (sourceArray.Length == 0) return true;

        var indices = new int[rank];
        Array.Copy(lowerBounds, indices, rank);

        while (true)
        {
            object value = sourceArray.GetValue(indices);
            if (!TryCloneInternal(value, out object clonedValue, visited)) return false;
            clonedArray.SetValue(clonedValue, indices);

            int d = rank - 1;
            while (d >= 0)
            {
                indices[d]++;
                if (indices[d] < lowerBounds[d] + lengths[d]) break;
                indices[d] = lowerBounds[d];
                d--;
            }

            if (d < 0) break;
        }

        return true;
    }

    /// <summary>
    /// Attempts to create an instance of <paramref name="type"/> for cloning.
    /// </summary>
    private static bool TryCreateInstance(Type type, out object instance)
    {
        instance = null;
        if (type.IsInterface || type.IsAbstract) return false;

        var factory = ObjectFactoryCache.GetOrAdd(type, BuildFactory);
        if (factory == null) return false;

        try
        {
            instance = factory();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds a factory used to instantiate a specific type during cloning.
    /// </summary>
    private static Func<object> BuildFactory(Type type)
    {
        if (type.IsValueType)
        {
            try
            {
                var body = Expression.Convert(Expression.Default(type), typeof(object));
                return Expression.Lambda<Func<object>>(body).Compile();
            }
            catch
            {
                return () => Activator.CreateInstance(type);
            }
        }

        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(c => c.GetParameters().Length == 0);

        if (ctor != null)
        {
            var compiledFactory = TryBuildCtorFactory(ctor);
            if (compiledFactory != null) return compiledFactory;

            // Fallback for cases where expression compilation cannot access the ctor.
            return () => ctor.Invoke(Array.Empty<object>());
        }

#pragma warning disable SYSLIB0050
        return () => FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
    }

    private static Func<object> TryBuildCtorFactory(ConstructorInfo ctor)
    {
        try
        {
            var body = Expression.Convert(Expression.New(ctor), typeof(object));
            return Expression.Lambda<Func<object>>(body).Compile();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all instance fields (public/non-public, including inherited) for a type.
    /// </summary>
    private static FieldInfo[] GetAllInstanceFields(Type type)
    {
        return FieldsCache.GetOrAdd(type, t =>
        {
            var fields = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            while (t != null)
            {
                fields.AddRange(t.GetFields(flags));
                t = t.BaseType;
            }

            return fields.ToArray();
        });
    }

    /// <summary>
    /// Determines whether a type is treated as immutable for cloning.
    /// </summary>
    private static bool IsImmutable(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type == typeof(Uri) ||
               typeof(Type).IsAssignableFrom(type) ||
               typeof(Delegate).IsAssignableFrom(type) ||
               (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                IsImmutable(Nullable.GetUnderlyingType(type)));
    }

    private static TaskCloneHandling GetTaskCloneHandling(IDictionary<object, object> visited)
    {
        if (visited.TryGetValue(TaskCloneHandlingKey, out var value) && value is TaskCloneHandling handling)
        {
            return handling;
        }

        return TaskCloneHandling.CopyReference;
    }

    private static bool TryCloneTaskObject(object sourceObj, IDictionary<object, object> visited, out object cloneObj)
    {
        if (sourceObj == null)
        {
            cloneObj = null;
            return true;
        }

        if (!(sourceObj is Task sourceTask))
        {
            cloneObj = sourceObj;
            return true;
        }

        var handling = GetTaskCloneHandling(visited);
        if (handling == TaskCloneHandling.CopyReference)
        {
            cloneObj = sourceObj;
            return true;
        }

        var sourceType = sourceObj.GetType();

        // Non-generic Task has no result payload to clone.
        if (!sourceType.IsGenericType || sourceType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            cloneObj = sourceObj;
            return true;
        }

        // Only clone completed successful Task<T>; keep all other tasks as reference.
        if (sourceTask.Status != TaskStatus.RanToCompletion)
        {
            cloneObj = sourceObj;
            return true;
        }

        if (visited.TryGetValue(sourceObj, out var existing))
        {
            cloneObj = existing;
            return true;
        }

        var cloner = TaskResultClonerCache.GetOrAdd(sourceType, BuildTaskResultCloner);
        return cloner(sourceTask, visited, out cloneObj);
    }

    private static TaskResultClonerFunction BuildTaskResultCloner(Type taskType)
    {
        var resultType = taskType.GetGenericArguments()[0];
        var method = typeof(DeepCloner)
            .GetMethod(nameof(TryCloneCompletedTaskWithResultBridge), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(resultType);

        return (TaskResultClonerFunction)Delegate.CreateDelegate(typeof(TaskResultClonerFunction), method);
    }

    private static bool TryCloneCompletedTaskWithResultBridge<TResult>(Task sourceTask, IDictionary<object, object> visited, out object cloneTaskObj)
    {
        if (!TryCloneCompletedTaskWithResult((Task<TResult>)sourceTask, visited, out Task<TResult> cloneTask))
        {
            cloneTaskObj = null;
            return false;
        }

        cloneTaskObj = cloneTask;
        return true;
    }

    private static bool TryCloneCompletedTaskWithResult<TResult>(Task<TResult> sourceTask, IDictionary<object, object> visited, out Task<TResult> cloneTask)
    {
        if (visited.TryGetValue(sourceTask, out var existing))
        {
            cloneTask = (Task<TResult>)existing;
            return true;
        }

        var tcs = new TaskCompletionSource<TResult>();
        visited[sourceTask] = tcs.Task;

        if (!TryCloneInternal(sourceTask.Result, out TResult clonedResult, visited))
        {
            cloneTask = null;
            return false;
        }

        tcs.SetResult(clonedResult);
        cloneTask = tcs.Task;
        visited[sourceTask] = cloneTask;
        return true;
    }

    /// <summary>
    /// Delegate signature for strongly typed clone functions.
    /// </summary>
    private delegate bool TryCloneFunction<T>(T source, IDictionary<object, object> visited, out T clone);

    /// <summary>
    /// Delegate signature for runtime-dispatched clone functions.
    /// </summary>
    private delegate bool RuntimeClonerFunction(object source, IDictionary<object, object> visited, out object clone);

    /// <summary>
    /// Delegate signature for compiled field-cloning functions.
    /// </summary>
    private delegate bool CloneFieldsFunction<T>(T source, T target, IDictionary<object, object> visited, out T clone);

    /// <summary>
    /// Delegate signature for SzArray cloning.
    /// </summary>
    private delegate bool SzArrayClonerFunction(Array source, IDictionary<object, object> visited, out Array clone);

    /// <summary>
    /// Delegate signature for cloning generic lists.
    /// </summary>
    private delegate bool GenericListClonerFunction(object source, IDictionary<object, object> visited, out object clone);

    /// <summary>
    /// Delegate signature for cloning generic dictionaries.
    /// </summary>
    private delegate bool GenericDictionaryClonerFunction(object source, IDictionary<object, object> visited, out object clone);

    /// <summary>
    /// Delegate signature for cloning completed Task&lt;T&gt; results.
    /// </summary>
    private delegate bool TaskResultClonerFunction(Task source, IDictionary<object, object> visited, out object clone);

    /// <summary>
    /// Per-type clone delegate cache.
    /// </summary>
    private static class ClonerCache<T>
    {
        /// <summary>
        /// Gets the cached clone function for <typeparamref name="T"/>.
        /// </summary>
        public static readonly TryCloneFunction<T> TryClone = Build();

        /// <summary>
        /// Builds a type-specialized clone function for <typeparamref name="T"/>.
        /// </summary>
        private static TryCloneFunction<T> Build()
        {
            var type = typeof(T);

            if (IsImmutable(type))
            {
                return (T source, IDictionary<object, object> visited, out T clone) =>
                {
                    clone = source;
                    return true;
                };
            }

            if (typeof(Task).IsAssignableFrom(type))
            {
                return (T source, IDictionary<object, object> visited, out T clone) =>
                {
                    if (!TryCloneTaskObject(source, visited, out object clonedObj))
                    {
                        clone = default;
                        return false;
                    }

                    clone = clonedObj is null ? default : (T)clonedObj;
                    return true;
                };
            }

            if (type == typeof(object))
            {
                return (T source, IDictionary<object, object> visited, out T clone) =>
                {
                    if (!TryCloneInternal((object)source, out object boxedClone, visited))
                    {
                        clone = default;
                        return false;
                    }

                    clone = boxedClone is null ? default : (T)boxedClone;
                    return true;
                };
            }

            if (type.IsArray)
            {
                return (T source, IDictionary<object, object> visited, out T clone) =>
                {
                    if (source is null)
                    {
                        clone = default;
                        return true;
                    }

                    if (!TryCloneArray((Array)(object)source, out var clonedArray, visited))
                    {
                        clone = default;
                        return false;
                    }

                    clone = (T)(object)clonedArray;
                    return true;
                };
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                return (T source, IDictionary<object, object> visited, out T clone) =>
                {
                    if (source is null)
                    {
                        clone = default;
                        return true;
                    }

                    object sourceObj = source;
                    if (visited.TryGetValue(sourceObj, out object existing))
                    {
                        clone = (T)existing;
                        return true;
                    }

                    if (!TryCreateInstance(type, out object targetObj))
                    {
                        clone = default;
                        return false;
                    }

                    visited[sourceObj] = targetObj;

                    var sourceDictionary = (IDictionary)sourceObj;
                    var targetDictionary = (IDictionary)targetObj;

                    foreach (DictionaryEntry entry in sourceDictionary)
                    {
                        if (!TryCloneInternal(entry.Key, out object clonedKey, visited))
                        {
                            clone = default;
                            return false;
                        }

                        if (!TryCloneInternal(entry.Value, out object clonedValue, visited))
                        {
                            clone = default;
                            return false;
                        }

                        targetDictionary.Add(clonedKey, clonedValue);
                    }

                    clone = (T)targetObj;
                    return true;
                };
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                return (T source, IDictionary<object, object> visited, out T clone) =>
                {
                    if (source is null)
                    {
                        clone = default;
                        return true;
                    }

                    object sourceObj = source;
                    if (visited.TryGetValue(sourceObj, out object existing))
                    {
                        clone = (T)existing;
                        return true;
                    }

                    if (!TryCreateInstance(type, out object targetObj))
                    {
                        clone = default;
                        return false;
                    }

                    visited[sourceObj] = targetObj;

                    var sourceList = (IList)sourceObj;
                    var targetList = (IList)targetObj;

                    foreach (var item in sourceList)
                    {
                        if (!TryCloneInternal(item, out object clonedItem, visited))
                        {
                            clone = default;
                            return false;
                        }

                        targetList.Add(clonedItem);
                    }

                    clone = (T)targetObj;
                    return true;
                };
            }

            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();

                if (genericTypeDef == typeof(List<>))
                {
                    var listCloner = GenericListClonerCache.GetOrAdd(type, BuildGenericListCloner);

                    return (T source, IDictionary<object, object> visited, out T clone) =>
                    {
                        if (!listCloner(source, visited, out object clonedObj))
                        {
                            clone = default;
                            return false;
                        }

                        clone = clonedObj is null ? default : (T)clonedObj;
                        return true;
                    };
                }

                if (genericTypeDef == typeof(Dictionary<,>))
                {
                    var dictionaryCloner = GenericDictionaryClonerCache.GetOrAdd(type, BuildGenericDictionaryCloner);

                    return (T source, IDictionary<object, object> visited, out T clone) =>
                    {
                        if (!dictionaryCloner(source, visited, out object clonedObj))
                        {
                            clone = default;
                            return false;
                        }

                        clone = clonedObj is null ? default : (T)clonedObj;
                        return true;
                    };
                }
            }

            return type.IsValueType ? ValueTypeClonerCache<T>.TryClone : ReferenceTypeClonerCache<T>.TryClone;
        }
    }

    /// <summary>
    /// Per-reference-type clone delegate cache.
    /// </summary>
    private static class ReferenceTypeClonerCache<T>
    {
        /// <summary>
        /// Gets the cached reference-type clone function.
        /// </summary>
        public static readonly TryCloneFunction<T> TryClone = Build();

        /// <summary>
        /// Builds a clone function for reference types.
        /// </summary>
        private static TryCloneFunction<T> Build()
        {
            var type = typeof(T);

            return (T source, IDictionary<object, object> visited, out T clone) =>
            {
                if (source is null)
                {
                    clone = default;
                    return true;
                }

                object sourceObj = source;
                if (visited.TryGetValue(sourceObj, out object existing))
                {
                    clone = (T)existing;
                    return true;
                }

                if (!TryCreateInstance(type, out object targetObj))
                {
                    clone = default;
                    return false;
                }

                visited[sourceObj] = targetObj;
                if (!ReferenceFieldClonerCache<T>.CloneFields(source, (T)targetObj, visited, out clone)) return false;
                return true;
            };
        }
    }

    /// <summary>
    /// Builds and caches expression-compiled field cloners for reference types.
    /// </summary>
    private static class ReferenceFieldClonerCache<T>
    {
        /// <summary>
        /// Gets the cached compiled field cloner.
        /// </summary>
        public static readonly CloneFieldsFunction<T> CloneFields = Build();

        /// <summary>
        /// Creates an expression-compiled function that clones all writable fields.
        /// </summary>
        private static CloneFieldsFunction<T> Build()
        {
            var type = typeof(T);

            var source = Expression.Parameter(type, "source");
            var target = Expression.Parameter(type, "target");
            var visited = Expression.Parameter(typeof(IDictionary<object, object>), "visited");
            var cloneOut = Expression.Parameter(type.MakeByRefType(), "clone");

            var returnLabel = Expression.Label(typeof(bool), "returnLabel");
            var variables = new List<ParameterExpression>();
            var expressions = new List<Expression>();

            foreach (var field in GetAllInstanceFields(type))
            {
                if (field.IsStatic) continue;

                var clonedField = Expression.Variable(field.FieldType, "cloned_" + field.Name);
                variables.Add(clonedField);

                var tryCloneField = Expression.Call(
                    GenericTryCloneInternalMethod.MakeGenericMethod(field.FieldType),
                    Expression.Field(source, field),
                    clonedField,
                    visited);

                expressions.Add(Expression.IfThen(
                    Expression.IsFalse(tryCloneField),
                    Expression.Block(
                        Expression.Assign(cloneOut, Expression.Default(type)),
                        Expression.Return(returnLabel, Expression.Constant(false)))));

                if (field.IsInitOnly)
                {
                    var setReadonly = Expression.Call(
                        SetInstanceFieldMethod,
                        Expression.Convert(target, typeof(object)),
                        Expression.Constant(field, typeof(FieldInfo)),
                        Expression.Convert(clonedField, typeof(object)));

                    expressions.Add(Expression.IfThen(
                        Expression.IsFalse(setReadonly),
                        Expression.Block(
                            Expression.Assign(cloneOut, Expression.Default(type)),
                            Expression.Return(returnLabel, Expression.Constant(false)))));
                }
                else
                {
                    expressions.Add(Expression.Assign(Expression.Field(target, field), clonedField));
                }

                if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                {
                    expressions.Add(Expression.Call(
                        RegisterDelegateFieldForRebindingMethod,
                        visited,
                        Expression.Convert(target, typeof(object)),
                        Expression.Constant(field, typeof(FieldInfo)),
                        Expression.Convert(clonedField, typeof(Delegate))));
                }
            }

            expressions.Add(Expression.Assign(cloneOut, target));
            expressions.Add(Expression.Label(returnLabel, Expression.Constant(true)));

            var body = Expression.Block(variables, expressions);
            return Expression.Lambda<CloneFieldsFunction<T>>(body, source, target, visited, cloneOut).Compile();
        }
    }

    /// <summary>
    /// Builds and caches expression-compiled cloners for value types.
    /// </summary>
    private static class ValueTypeClonerCache<T>
    {
        /// <summary>
        /// Gets the cached value-type clone function.
        /// </summary>
        public static readonly TryCloneFunction<T> TryClone = Build();

        /// <summary>
        /// Creates a clone function for value types.
        /// </summary>
        private static TryCloneFunction<T> Build()
        {
            var type = typeof(T);

            if (!type.IsValueType || IsImmutable(type))
            {
                return (T source, IDictionary<object, object> visited, out T clone) =>
                {
                    clone = source;
                    return true;
                };
            }

            var source = Expression.Parameter(type, "source");
            var visited = Expression.Parameter(typeof(IDictionary<object, object>), "visited");
            var cloneOut = Expression.Parameter(type.MakeByRefType(), "clone");

            var cloneVar = Expression.Variable(type, "cloneVar");
            var returnLabel = Expression.Label(typeof(bool), "returnLabel");

            var variables = new List<ParameterExpression> { cloneVar };
            var expressions = new List<Expression>
            {
                Expression.Assign(cloneVar, source)
            };

            foreach (var field in GetAllInstanceFields(type))
            {
                if (field.IsStatic) continue;

                var clonedField = Expression.Variable(field.FieldType, "cloned_" + field.Name);
                variables.Add(clonedField);

                var tryCloneField = Expression.Call(
                    GenericTryCloneInternalMethod.MakeGenericMethod(field.FieldType),
                    Expression.Field(source, field),
                    clonedField,
                    visited);

                expressions.Add(Expression.IfThen(
                    Expression.IsFalse(tryCloneField),
                    Expression.Block(
                        Expression.Assign(cloneOut, Expression.Default(type)),
                        Expression.Return(returnLabel, Expression.Constant(false)))));

                if (field.IsInitOnly)
                {
                    var setReadonlyStruct = Expression.Call(
                        SetInitOnlyStructFieldGenericMethod.MakeGenericMethod(type),
                        cloneVar,
                        Expression.Constant(field, typeof(FieldInfo)),
                        Expression.Convert(clonedField, typeof(object)));

                    expressions.Add(Expression.IfThen(
                        Expression.IsFalse(setReadonlyStruct),
                        Expression.Block(
                            Expression.Assign(cloneOut, Expression.Default(type)),
                            Expression.Return(returnLabel, Expression.Constant(false)))));
                }
                else
                {
                    expressions.Add(Expression.Assign(Expression.Field(cloneVar, field), clonedField));
                }
            }

            expressions.Add(Expression.Assign(cloneOut, cloneVar));
            expressions.Add(Expression.Label(returnLabel, Expression.Constant(true)));

            var body = Expression.Block(variables, expressions);
            return Expression.Lambda<TryCloneFunction<T>>(body, source, visited, cloneOut).Compile();
        }
    }

    /// <summary>
    /// Reference-based equality comparer used for visited-object tracking.
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer() { }

        /// <inheritdoc />
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        /// <inheritdoc />
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Cached generic method info for <c>TryCloneInternal&lt;T&gt;(T, out T, IDictionary&lt;object,object&gt;)</c>.
    /// </summary>
    private static readonly MethodInfo GenericTryCloneInternalMethod =
        typeof(DeepCloner).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == nameof(TryCloneInternal) &&
                         m.IsGenericMethodDefinition &&
                         m.GetParameters().Length == 3);

    /// <summary>
    /// Pool of visited maps used to track already-cloned reference objects.
    /// </summary>
    private static readonly Pool<Dictionary<object, object>> visitedPool =
        new Pool<Dictionary<object, object>>(
            () => new Dictionary<object, object>(ReferenceEqualityComparer.Instance),
            d => d.Clear(),
            100,
            true);

    /// <summary>
    /// Runtime-type cloner cache for <see cref="object"/> dispatch.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, RuntimeClonerFunction> RuntimeClonerCache = new ConcurrentDictionary<Type, RuntimeClonerFunction>();

    /// <summary>
    /// Cache for resolved instance fields per type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldsCache = new ConcurrentDictionary<Type, FieldInfo[]>();

    /// <summary>
    /// Factory cache for object creation during clone materialization.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<object>> ObjectFactoryCache = new ConcurrentDictionary<Type, Func<object>>();

    private static readonly ConcurrentDictionary<Type, SzArrayClonerFunction> SzArrayClonerCache = new ConcurrentDictionary<Type, SzArrayClonerFunction>();
    private static readonly ConcurrentDictionary<Type, GenericListClonerFunction> GenericListClonerCache = new ConcurrentDictionary<Type, GenericListClonerFunction>();
    private static readonly ConcurrentDictionary<Type, GenericDictionaryClonerFunction> GenericDictionaryClonerCache = new ConcurrentDictionary<Type, GenericDictionaryClonerFunction>();
    private static readonly ConcurrentDictionary<Type, TaskResultClonerFunction> TaskResultClonerCache = new ConcurrentDictionary<Type, TaskResultClonerFunction>();

    private static bool TrySetInstanceField(object target, FieldInfo field, object value)
    {
        try
        {
            field.SetValue(target, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySetInitOnlyStructField<TStruct>(ref TStruct target, FieldInfo field, object value) where TStruct : struct
    {
        try
        {
            object boxed = target;
            field.SetValue(boxed, value);
            target = (TStruct)boxed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static readonly MethodInfo SetInstanceFieldMethod =
        typeof(DeepCloner).GetMethod(nameof(TrySetInstanceField), BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly MethodInfo SetInitOnlyStructFieldGenericMethod =
        typeof(DeepCloner).GetMethod(nameof(TrySetInitOnlyStructField), BindingFlags.NonPublic | BindingFlags.Static);

    private static SzArrayClonerFunction BuildSzArrayCloner(Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        var method = typeof(DeepCloner)
            .GetMethod(nameof(TryCloneSzArrayBridge), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(elementType);

        return (SzArrayClonerFunction)Delegate.CreateDelegate(typeof(SzArrayClonerFunction), method);
    }

    private static bool TryCloneSzArrayBridge<TElement>(Array source, IDictionary<object, object> visited, out Array clone)
    {
        if (!TryCloneSzArray((TElement[])source, out TElement[] typedClone, visited))
        {
            clone = null;
            return false;
        }

        clone = typedClone;
        return true;
    }

    private static bool TryCloneSzArray<TElement>(TElement[] sourceArray, out TElement[] clonedArray, IDictionary<object, object> visited)
    {
        if (visited.TryGetValue(sourceArray, out object existingArray))
        {
            clonedArray = (TElement[])existingArray;
            return true;
        }

        clonedArray = new TElement[sourceArray.Length];
        visited[sourceArray] = clonedArray;

        for (int i = 0; i < sourceArray.Length; i++)
        {
            if (!TryCloneInternal(sourceArray[i], out TElement clonedValue, visited))
            {
                clonedArray = null;
                return false;
            }

            clonedArray[i] = clonedValue;
        }

        return true;
    }

    private static GenericListClonerFunction BuildGenericListCloner(Type listType)
    {
        var elementType = listType.GetGenericArguments()[0];
        var method = typeof(DeepCloner)
            .GetMethod(nameof(TryCloneListBridge), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(elementType);

        return (GenericListClonerFunction)Delegate.CreateDelegate(typeof(GenericListClonerFunction), method);
    }

    private static bool TryCloneListBridge<TElement>(object source, IDictionary<object, object> visited, out object clone)
    {
        if (!TryCloneList((List<TElement>)source, out List<TElement> typedClone, visited))
        {
            clone = null;
            return false;
        }

        clone = typedClone;
        return true;
    }

    private static bool TryCloneList<TElement>(List<TElement> source, out List<TElement> clone, IDictionary<object, object> visited)
    {
        if (source == null)
        {
            clone = null;
            return true;
        }

        if (visited.TryGetValue(source, out object existing))
        {
            clone = (List<TElement>)existing;
            return true;
        }

        clone = new List<TElement>(source.Count);
        visited[source] = clone;

        for (int i = 0; i < source.Count; i++)
        {
            if (!TryCloneInternal(source[i], out TElement clonedItem, visited))
            {
                clone = null;
                return false;
            }

            clone.Add(clonedItem);
        }

        return true;
    }

    private static GenericDictionaryClonerFunction BuildGenericDictionaryCloner(Type dictionaryType)
    {
        var args = dictionaryType.GetGenericArguments();
        var method = typeof(DeepCloner)
            .GetMethod(nameof(TryCloneDictionaryBridge), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(args[0], args[1]);

        return (GenericDictionaryClonerFunction)Delegate.CreateDelegate(typeof(GenericDictionaryClonerFunction), method);
    }

    private static bool TryCloneDictionaryBridge<TKey, TValue>(object source, IDictionary<object, object> visited, out object clone)
    {
        if (!TryCloneDictionary((Dictionary<TKey, TValue>)source, out Dictionary<TKey, TValue> typedClone, visited))
        {
            clone = null;
            return false;
        }

        clone = typedClone;
        return true;
    }

    private static bool TryCloneDictionary<TKey, TValue>(Dictionary<TKey, TValue> source, out Dictionary<TKey, TValue> clone, IDictionary<object, object> visited)
    {
        if (source == null)
        {
            clone = null;
            return true;
        }

        if (visited.TryGetValue(source, out object existing))
        {
            clone = (Dictionary<TKey, TValue>)existing;
            return true;
        }

        clone = new Dictionary<TKey, TValue>(source.Count, source.Comparer);
        visited[source] = clone;

        foreach (var kv in source)
        {
            if (!TryCloneInternal(kv.Key, out TKey clonedKey, visited))
            {
                clone = null;
                return false;
            }

            if (!TryCloneInternal(kv.Value, out TValue clonedValue, visited))
            {
                clone = null;
                return false;
            }

            clone.Add(clonedKey, clonedValue);
        }

        return true;
    }

    private static bool TryFinalizeDelegateRebinding(IDictionary<object, object> visited, bool rebindAllTargets)
    {
        if (!visited.TryGetValue(DelegateRebindWorklistKey, out var worklistObj))
        {
            return true;
        }

        var worklist = (List<DelegateRebindEntry>)worklistObj;
        for (int i = 0; i < worklist.Count; i++)
        {
            var entry = worklist[i];

            if (!TryRebindDelegate(entry.SourceDelegate, visited, rebindAllTargets, out var reboundDelegate))
            {
                return false;
            }

            if (!TrySetFieldValue(entry.CloneTarget, entry.Field, reboundDelegate))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryRebindDelegate(Delegate source, IDictionary<object, object> visited, bool rebindAllTargets, out Delegate rebound)
    {
        if (source == null)
        {
            rebound = null;
            return true;
        }

        var invocationList = source.GetInvocationList();
        Delegate combined = null;

        for (int i = 0; i < invocationList.Length; i++)
        {
            var handler = invocationList[i];
            Delegate reboundHandler;

            if (handler.Target == null)
            {
                // static/open delegate
                reboundHandler = handler;
            }
            else if (visited.TryGetValue(handler.Target, out var clonedTarget))
            {
                try
                {
                    reboundHandler = Delegate.CreateDelegate(handler.GetType(), clonedTarget, handler.Method, true);
                }
                catch
                {
                    rebound = null;
                    return false;
                }
            }
            else if (rebindAllTargets)
            {
                if (!TryCloneInternal(handler.Target, out object clonedExternalTarget, visited))
                {
                    rebound = null;
                    return false;
                }

                try
                {
                    reboundHandler = Delegate.CreateDelegate(handler.GetType(), clonedExternalTarget, handler.Method, true);
                }
                catch
                {
                    rebound = null;
                    return false;
                }
            }
            else
            {
                // external target: keep original binding
                reboundHandler = handler;
            }

            combined = combined == null ? reboundHandler : Delegate.Combine(combined, reboundHandler);
        }

        rebound = combined;
        return true;
    }

    private static bool TrySetFieldValue(object target, FieldInfo field, object value)
    {
        if (field.IsInitOnly)
        {
            return TrySetInstanceField(target, field, value);
        }

        try
        {
            field.SetValue(target, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class DelegateRebindEntry
    {
        public DelegateRebindEntry(object cloneTarget, FieldInfo field, Delegate sourceDelegate)
        {
            CloneTarget = cloneTarget;
            Field = field;
            SourceDelegate = sourceDelegate;
        }

        public object CloneTarget { get; }
        public FieldInfo Field { get; }
        public Delegate SourceDelegate { get; }
    }

    private static readonly Pool<List<DelegateRebindEntry>> delegateRebindWorklistPool =
        new Pool<List<DelegateRebindEntry>>(
            () => new List<DelegateRebindEntry>(8),
            list => list.Clear(),
            100,
            true);

    private static readonly MethodInfo RegisterDelegateFieldForRebindingMethod =
        typeof(DeepCloner).GetMethod(nameof(RegisterDelegateFieldForRebinding), BindingFlags.NonPublic | BindingFlags.Static);

    private static void RegisterDelegateFieldForRebinding(
        IDictionary<object, object> visited,
        object cloneTarget,
        FieldInfo field,
        Delegate sourceDelegate)
    {
        if (sourceDelegate == null) return;
        if (!visited.ContainsKey(DelegateRebindEnabledKey)) return;

        if (!visited.TryGetValue(DelegateRebindWorklistKey, out var worklistObj))
        {
            worklistObj = delegateRebindWorklistPool.Take();
            visited[DelegateRebindWorklistKey] = worklistObj;
        }

        ((List<DelegateRebindEntry>)worklistObj).Add(new DelegateRebindEntry(cloneTarget, field, sourceDelegate));
    }

    private static readonly object DelegateRebindEnabledKey = new object();
    private static readonly object DelegateRebindWorklistKey = new object();
    private static readonly object TaskCloneHandlingKey = new object();
}