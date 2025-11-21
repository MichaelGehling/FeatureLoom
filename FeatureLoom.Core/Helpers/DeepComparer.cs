using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;

namespace FeatureLoom.Helpers
{
    /// <summary>
    /// Provides deep structural equality comparison for arbitrary object graphs, including support for cycles, collections, and dictionaries.
    /// </summary>
    public static class DeepComparer
    {
        static Pool<HashSet<ReferencePair>> visitedPool = new Pool<HashSet<ReferencePair>>(() => new HashSet<ReferencePair>(ReferencePair.Comparer), h => h.Clear(), 100, true);

        /// <summary>
        /// Compares two objects deeply for structural equality.
        /// Handles nested objects, collections, dictionaries, and cyclic references.
        /// </summary>
        /// <typeparam name="T">Type of the objects to compare.</typeparam>
        /// <param name="x">First object to compare.</param>
        /// <param name="y">Second object to compare.</param>
        /// <param name="strictTypeCheck">
        /// If true, objects must be of exactly the same runtime type to be considered equal.
        /// If false, allows comparison of objects with compatible types.
        /// </param>
        /// <returns>True if the objects are deeply equal; otherwise, false.</returns>
        public static bool AreEqual<T>(T x, T y, bool strictTypeCheck = true)
        {
            if (strictTypeCheck && x != null && y != null && x.GetType() != y.GetType()) return false;

            bool? result = BasicChecks(x, y);
            if (result.HasValue) return result.Value;

            var visited = visitedPool.Take();
            result = InternalDeepEquals(x, y, visited);
            visitedPool.Return(visited);
            return result.Value;
        }

        /// <summary>
        /// Extension method for deep structural equality comparison.
        /// Equivalent to <see cref="AreEqual{T}(T, T, bool)"/> with default strict type check.
        /// </summary>
        /// <typeparam name="T">Type of the objects to compare.</typeparam>
        /// <param name="x">First object to compare.</param>
        /// <param name="y">Second object to compare.</param>
        /// <returns>True if the objects are deeply equal; otherwise, false.</returns>
        public static bool EqualsDeep<T>(this T x, T y) => AreEqual(x, y);

        #region Internal Comparison Methods

        /// <summary>
        /// Recursively compares two objects for deep structural equality.
        /// Uses a visited set to avoid infinite recursion on cyclic graphs.
        /// </summary>
        /// <typeparam name="T">Type of the objects to compare.</typeparam>
        /// <param name="x">First object to compare.</param>
        /// <param name="y">Second object to compare.</param>
        /// <param name="visited">Set of already compared object pairs for cycle detection.</param>
        /// <returns>True if the objects are deeply equal; otherwise, false.</returns>
        internal static bool InternalDeepEquals<T>(T x, T y, ISet<ReferencePair> visited)
        {
            bool? result = BasicChecks(x, y);
            if (result.HasValue) return result.Value;       

            // Cycle detection: record the current pair (by reference).
            var pair = new ReferencePair(x, y);
            if (!visited.Add(pair)) return true; // already compared this pair

            // Retrieve or create a compiled delegate for type T.
            var comparer = ComparerCache<T>.GetOrCreateComparer();
            return comparer(x, y, visited);
        }

        private static bool? BasicChecks<T>(T x, T y)
        {
            // Check for reference equality or both null.
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            // For primitive (or value–like) types, use their Equals.
            if (IsPrimitive(typeof(T))) return x.Equals(y);
            return null;
        }

        /// <summary>
        /// Compares two <see cref="IEnumerable"/> instances element-by-element for deep equality.
        /// Handles collections of arbitrary types and supports nested structures.
        /// </summary>
        /// <param name="a">First enumerable to compare.</param>
        /// <param name="b">Second enumerable to compare.</param>
        /// <param name="visited">Set of already compared object pairs for cycle detection.</param>
        /// <returns>True if the enumerables are deeply equal; otherwise, false.</returns>
        internal static bool CompareEnumerables(IEnumerable a, IEnumerable b, ISet<ReferencePair> visited)
        {
            if (a == null || b == null)
                return a == b;

            var enumA = a.GetEnumerator();
            var enumB = b.GetEnumerator();

            while (true)
            {
                bool movedA = enumA.MoveNext();
                bool movedB = enumB.MoveNext();

                if (movedA != movedB)
                    return false; // different length

                if (!movedA) // end reached in both
                    break;

                if (!InternalDeepEquals(enumA.Current, enumB.Current, visited))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Optimized helper for comparing two <see cref="IList{T}"/> instances.
        /// Compares count first, then iterates by index for deep equality.
        /// </summary>
        /// <typeparam name="TItem">Type of the list elements.</typeparam>
        /// <param name="a">First list to compare.</param>
        /// <param name="b">Second list to compare.</param>
        /// <param name="visited">Set of already compared object pairs for cycle detection.</param>
        /// <returns>True if the lists are deeply equal; otherwise, false.</returns>
        internal static bool CompareLists<TItem>(IList<TItem> a, IList<TItem> b, ISet<ReferencePair> visited)
        {
            if (a == null || b == null)
                return a == b;

            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!InternalDeepEquals(a[i], b[i], visited))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Optimized helper for comparing two <see cref="IDictionary{TKey, TValue}"/> instances in an order-independent manner.
        /// Keys are compared deeply, not just by reference or default equality.
        /// </summary>
        /// <typeparam name="TKey">Type of the dictionary keys.</typeparam>
        /// <typeparam name="TValue">Type of the dictionary values.</typeparam>
        /// <param name="a">First dictionary to compare.</param>
        /// <param name="b">Second dictionary to compare.</param>
        /// <param name="visited">Set of already compared object pairs for cycle detection.</param>
        /// <returns>True if the dictionaries are deeply equal; otherwise, false.</returns>
        internal static bool CompareDictionaries<TKey, TValue>(IDictionary<TKey, TValue> a, IDictionary<TKey, TValue> b, ISet<ReferencePair> visited)
        {
            if (a == null || b == null)
                return a == b;

            if (a.Count != b.Count)
                return false;

            // For each key/value in a, ensure b contains a deep–equal key and matching value.
            foreach (var kvp in a)
            {
                // Check if b contains an equivalent key.
                if (!b.TryGetValue(kvp.Key, out var bValue))
                {
                    // The key might not be found using standard equality.
                    bool found = false;
                    foreach (var key in b.Keys)
                    {
                        if (InternalDeepEquals(kvp.Key, key, visited))
                        {
                            bValue = b[key];
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return false;
                }
                if (!InternalDeepEquals(kvp.Value, bValue, visited))
                    return false;
            }
            return true;
        }

        #endregion

        #region Primitive & Reflection Helpers

        /// <summary>
        /// Determines whether the specified type is considered "primitive" for deep comparison purposes.
        /// This includes built-in primitives, enums, strings, decimals, date/time types, GUIDs, URIs, and nullable primitives.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>True if the type is treated as primitive; otherwise, false.</returns>
        private static bool IsPrimitive(Type type)
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
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                    IsPrimitive(Nullable.GetUnderlyingType(type)));
        }

        /// <summary>
        /// Caches all instance fields (public, non-public, and inherited) for a given type to optimize reflection.
        /// </summary>
        internal static class ReflectionCache
        {
            private static readonly ConcurrentDictionary<Type, FieldInfo[]> Cache = new();

            /// <summary>
            /// Gets all instance fields for the specified type, including inherited and non-public fields.
            /// </summary>
            /// <param name="type">Type whose fields to retrieve.</param>
            /// <returns>Array of <see cref="FieldInfo"/> representing all instance fields.</returns>
            public static FieldInfo[] GetAllFields(Type type)
            {
                return Cache.GetOrAdd(type, t =>
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    var fields = new List<FieldInfo>();
                    while (t != null)
                    {
                        fields.AddRange(t.GetFields(flags));
                        t = t.BaseType;
                    }
                    return fields.ToArray();
                });
            }
        }

        #endregion

        #region Delegate Cache

        /// <summary>
        /// Caches a compiled delegate for comparing two objects of type <typeparamref name="T"/> for deep equality.
        /// Uses expression trees for efficient repeated comparisons.
        /// </summary>
        /// <typeparam name="T">Type to compare.</typeparam>
        internal static class ComparerCache<T>
        {
            // Delegate signature: (T x, T y, ISet<ReferencePair> visited) => bool.
            internal static readonly Func<T, T, ISet<ReferencePair>, bool> Comparer = CreateComparer();

            /// <summary>
            /// Gets the cached or newly created comparer delegate for type <typeparamref name="T"/>.
            /// </summary>
            /// <returns>A delegate that deeply compares two objects of type <typeparamref name="T"/>.</returns>
            internal static Func<T, T, ISet<ReferencePair>, bool> GetOrCreateComparer() => Comparer;

            /// <summary>
            /// Builds an expression tree to compare two objects of type <typeparamref name="T"/> for deep equality.
            /// Handles primitives, collections, dictionaries, and nested objects.
            /// </summary>
            /// <returns>A compiled delegate for deep comparison.</returns>
            private static Func<T, T, ISet<ReferencePair>, bool> CreateComparer()
            {
                var type = typeof(T);

                // If T is primitive, simply use Equals.
                if (IsPrimitive(type))
                {
                    return (x, y, visited) => x.Equals(y);
                }

                // Special handling for dictionaries.
                var dictInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
                if (dictInterface != null)
                {
                    var genericArgs = dictInterface.GetGenericArguments();
                    var keyType = genericArgs[0];
                    var valueType = genericArgs[1];
                    var method = typeof(DeepComparer).GetMethod(nameof(CompareDictionaries), BindingFlags.NonPublic | BindingFlags.Static)
                        .MakeGenericMethod(keyType, valueType);
                    return (x, y, visited) => (bool)method.Invoke(null, new object[] { x, y, visited });
                }

                // Special handling for IEnumerable (but not string).
                if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
                {
                    // Try to use optimized IList<T> if available.
                    var ilistInterface = type.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
                    if (ilistInterface != null)
                    {
                        var itemType = ilistInterface.GetGenericArguments()[0];
                        var method = typeof(DeepComparer).GetMethod(nameof(CompareLists), BindingFlags.NonPublic | BindingFlags.Static)
                            .MakeGenericMethod(itemType);
                        return (x, y, visited) => (bool)method.Invoke(null, new object[] { x, y, visited });
                    }
                    // Otherwise fall back to generic enumerable comparison.
                    return (x, y, visited) => CompareEnumerables((IEnumerable)x, (IEnumerable)y, visited);
                }

                // Build lambda parameters: (T x, T y, ISet<ReferencePair> visited)
                var xParam = Expression.Parameter(type, "x");
                var yParam = Expression.Parameter(type, "y");
                var visitedParam = Expression.Parameter(typeof(ISet<ReferencePair>), "visited");

                // Get all instance fields.
                var fields = ReflectionCache.GetAllFields(type);
                // If no fields, always return true.
                if (fields.Length == 0)
                {
                    return Expression.Lambda<Func<T, T, ISet<ReferencePair>, bool>>(
                        Expression.Constant(true),
                        xParam, yParam, visitedParam).Compile();
                }

                // Build a chain of comparisons for each field.
                Expression body = Expression.Constant(true);

                foreach (var field in fields)
                {
                    var fieldType = field.FieldType;
                    Expression xField = Expression.Field(xParam, field);
                    Expression yField = Expression.Field(yParam, field);

                    Expression fieldComparison;

                    if (IsPrimitive(fieldType))
                    {
                        // Check if the field type is a non-nullable value type.
                        if (!fieldType.IsNullable())
                        {
                            // For non-nullable value types (like int), simply call Equals.
                            fieldComparison = Expression.Call(
                                xField,
                                fieldType.GetMethod("Equals", new Type[] { fieldType }),
                                yField
                            );
                        }
                        else
                        {
                            // For reference types or Nullable<T>, first check for nulls.
                            var nullValue = Expression.Constant(null, fieldType);
                            var bothNull = Expression.AndAlso(
                                Expression.Equal(xField, nullValue),
                                Expression.Equal(yField, nullValue)
                            );
                            var oneNull = Expression.OrElse(
                                Expression.Equal(xField, nullValue),
                                Expression.Equal(yField, nullValue)
                            );
                            var equalsCall = Expression.Call(
                                xField,
                                fieldType.GetMethod("Equals", new Type[] { fieldType }),
                                yField
                            );
                            fieldComparison = Expression.Condition(
                                bothNull,
                                Expression.Constant(true),
                                Expression.Condition(
                                    oneNull,
                                    Expression.Constant(false),
                                    equalsCall
                                )
                            );
                        }
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(fieldType) && fieldType != typeof(string))
                    {
                        // Handle dictionaries first.
                        var fieldDictInterface = fieldType.GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
                        if (fieldDictInterface != null)
                        {
                            var genericArgs = fieldDictInterface.GetGenericArguments();
                            var keyType = genericArgs[0];
                            var valueType = genericArgs[1];
                            var dictMethod = typeof(DeepComparer).GetMethod(nameof(CompareDictionaries), BindingFlags.NonPublic | BindingFlags.Static)
                                .MakeGenericMethod(keyType, valueType);
                            fieldComparison = Expression.Call(
                                dictMethod,
                                Expression.Convert(xField, fieldDictInterface),
                                Expression.Convert(yField, fieldDictInterface),
                                visitedParam);
                        }
                        else
                        {
                            // Try to use IList<T> optimization.
                            var ilistInterface = fieldType.GetInterfaces()
                                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
                            if (ilistInterface != null)
                            {
                                var itemType = ilistInterface.GetGenericArguments()[0];
                                var listMethod = typeof(DeepComparer).GetMethod(nameof(CompareLists), BindingFlags.NonPublic | BindingFlags.Static)
                                    .MakeGenericMethod(itemType);
                                fieldComparison = Expression.Call(
                                    listMethod,
                                    Expression.Convert(xField, ilistInterface),
                                    Expression.Convert(yField, ilistInterface),
                                    visitedParam);
                            }
                            else
                            {
                                // Fall back to comparing as IEnumerable.
                                fieldComparison = Expression.Call(
                                    typeof(DeepComparer).GetMethod(nameof(CompareEnumerables), BindingFlags.NonPublic | BindingFlags.Static),
                                    Expression.Convert(xField, typeof(IEnumerable)),
                                    Expression.Convert(yField, typeof(IEnumerable)),
                                    visitedParam);
                            }
                        }
                    }
                    else
                    {
                        // For all other types, recursively compare fields.
                        var method = typeof(DeepComparer).GetMethod(nameof(InternalDeepEquals), BindingFlags.NonPublic | BindingFlags.Static)
                            .MakeGenericMethod(fieldType);
                        fieldComparison = Expression.Call(method, xField, yField, visitedParam);
                    }
                    body = Expression.AndAlso(body, fieldComparison);
                }
                var lambda = Expression.Lambda<Func<T, T, ISet<ReferencePair>, bool>>(body, xParam, yParam, visitedParam);
                return lambda.Compile();
            }
        }

        #endregion

        #region Cycle Detection Helper

        /// <summary>
        /// Represents a pair of object references that have been compared, used for cycle detection.
        /// Ensures that the same object pair is not compared more than once, preventing infinite recursion.
        /// </summary>
        internal readonly struct ReferencePair
        {
            /// <summary>
            /// The first object in the pair.
            /// </summary>
            public object First { get; }
            /// <summary>
            /// The second object in the pair.
            /// </summary>
            public object Second { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ReferencePair"/> struct.
            /// Orders the pair by reference hash code to ensure symmetry.
            /// </summary>
            /// <param name="a">First object.</param>
            /// <param name="b">Second object.</param>
            public ReferencePair(object a, object b)
            {
                // Order the pair to ensure symmetry.
                if (RuntimeHelpers.GetHashCode(a) <= RuntimeHelpers.GetHashCode(b))
                {
                    First = a;
                    Second = b;
                }
                else
                {
                    First = b;
                    Second = a;
                }
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                if (obj is ReferencePair other)
                {
                    return ReferenceEquals(First, other.First) && ReferenceEquals(Second, other.Second);
                }
                return false;
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashFirst = RuntimeHelpers.GetHashCode(First);
                    int hashSecond = RuntimeHelpers.GetHashCode(Second);
                    return (hashFirst * 397) ^ hashSecond;
                }
            }

            /// <summary>
            /// Gets an equality comparer for <see cref="ReferencePair"/> that compares by reference.
            /// </summary>
            public static IEqualityComparer<ReferencePair> Comparer { get; } = new ReferencePairComparer();

            private class ReferencePairComparer : IEqualityComparer<ReferencePair>
            {
                public bool Equals(ReferencePair x, ReferencePair y)
                {
                    return ReferenceEquals(x.First, y.First) && ReferenceEquals(x.Second, y.Second);
                }

                public int GetHashCode(ReferencePair obj)
                {
                    return obj.GetHashCode();
                }
            }
        }

        #endregion
    }
}
