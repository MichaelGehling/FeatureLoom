using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Helpers
{
    public static class DeepComparer
    {
        /// <summary>
        /// Public API: compares two objects deeply for equality.
        /// </summary>
        public static bool AreEqual<T>(T x, T y, bool strictTypeCheck = true)
        {
            if (strictTypeCheck && x != null && y != null && x.GetType() != y.GetType())
                return false;
            var visited = new HashSet<ReferencePair>(ReferencePair.Comparer);
            return InternalDeepEquals(x, y, visited);
        }

        /// <summary>
        /// Extension method version.
        /// </summary>
        public static bool EqualsDeep<T>(this T x, T y) => AreEqual(x, y);

        #region Internal Comparison Methods

        /// <summary>
        /// Main entry point for deep comparison.
        /// </summary>
        internal static bool InternalDeepEquals<T>(T x, T y, ISet<ReferencePair> visited)
        {
            // Check for reference equality or both null.
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;

            // For primitive (or value–like) types, use their Equals.
            var type = typeof(T);
            if (IsPrimitive(type))
                return x.Equals(y);

            // Cycle detection: record the current pair (by reference).
            var pair = new ReferencePair(x, y);
            if (!visited.Add(pair))
                return true; // already compared this pair

            // Retrieve or create a compiled delegate for type T.
            var comparer = ComparerCache<T>.GetOrCreateComparer();
            return comparer(x, y, visited);
        }

        /// <summary>
        /// Compares two IEnumerable instances element–by–element.
        /// </summary>
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
        /// Optimized helper for comparing two IList&lt;TItem&gt; instances.
        /// Compares Count first, then iterates by index.
        /// </summary>
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
        /// Optimized helper for comparing two IDictionary&lt;TKey, TValue&gt; instances in an order–independent manner.
        /// </summary>
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
        /// Returns true if the type is considered “primitive” for deep comparison purposes.
        /// </summary>
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
        /// Caches all instance fields (public, non–public, inherited) for a given type.
        /// </summary>
        internal static class ReflectionCache
        {
            private static readonly ConcurrentDictionary<Type, FieldInfo[]> Cache = new();

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
        /// Caches a compiled delegate for comparing two objects of type T.
        /// </summary>
        internal static class ComparerCache<T>
        {
            // Delegate signature: (T x, T y, ISet<ReferencePair> visited) => bool.
            internal static readonly Func<T, T, ISet<ReferencePair>, bool> Comparer = CreateComparer();

            internal static Func<T, T, ISet<ReferencePair>, bool> GetOrCreateComparer() => Comparer;

            /// <summary>
            /// Builds an expression tree to compare two objects of type T.
            /// </summary>
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
                        // For primitives (and strings), call Equals.
                        fieldComparison = Expression.Call(
                            xField,
                            fieldType.GetMethod("Equals", new Type[] { fieldType }),
                            yField);
                    }
                    else if (fieldType == typeof(string))
                    {
                        // Strings use Equals.
                        fieldComparison = Expression.Call(xField, fieldType.GetMethod("Equals", new[] { fieldType }), yField);
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
        /// Records a pair of objects that have been compared to avoid infinite recursion.
        /// </summary>
        internal readonly struct ReferencePair
        {
            public object First { get; }
            public object Second { get; }

            public ReferencePair(object first, object second)
            {
                First = first;
                Second = second;
            }

            public override bool Equals(object obj)
            {
                if (obj is ReferencePair other)
                {
                    return ReferenceEquals(First, other.First) && ReferenceEquals(Second, other.Second);
                }
                return false;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashFirst = RuntimeHelpers.GetHashCode(First);
                    int hashSecond = RuntimeHelpers.GetHashCode(Second);
                    return (hashFirst * 397) ^ hashSecond;
                }
            }

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
