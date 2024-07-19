using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace FeatureLoom.Helpers
{
    public static class DeepComparer
    {        
        public static bool AreEqual<T>(T x, T y)
        {
            return GenericDeepComparer<T>.AreEqual(x, y);
        }

        public static bool EqualsDeep<T>(this T x, T y) => AreEqual(x, y);

        private static class GenericDeepComparer<T>
        {
            private static readonly Func<T, T, LazyValue<HashSet<Type>>, bool> defaultComparer = CreateDeepComparer(typeof(T));

            // Cache for all compiled comparer functions for inherited runtime types
            private static readonly LazyValue<Dictionary<Type, Func<T, T, LazyValue<HashSet<Type>>, bool>>> comparerCache = new();

            private static readonly FeatureLock cacheLock = new();

            public static bool AreEqual(T x, T y)
            {
                var visited = new LazyValue<HashSet<Type>>();
                return AreEqual(x, y, visited);
            }

            private static bool AreEqual(T x, T y, LazyValue<HashSet<Type>> visited)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;

                var runtimeType = x.GetType();
                if (runtimeType != y.GetType()) return false;

                var comparer = GetRuntimeTypeComparer(runtimeType);
                return comparer(x, y, visited);
            }

            private static Func<T, T, LazyValue<HashSet<Type>>, bool> GetRuntimeTypeComparer(Type runtimeType)
            {
                if (runtimeType == typeof(T)) return defaultComparer;

                using (cacheLock.Lock())
                {
                    if (!comparerCache.Obj.TryGetValue(runtimeType, out var comparer))
                    {
                        comparer = CreateDeepComparer(runtimeType);
                        comparerCache.Obj[runtimeType] = comparer;
                    }
                    return comparer;
                }
            }

            private static Func<T, T, LazyValue<HashSet<Type>>, bool> CreateDeepComparer(Type runtimeType)
            {
                var xParam = Expression.Parameter(typeof(T), "x");
                var yParam = Expression.Parameter(typeof(T), "y");
                var visitedParam = Expression.Parameter(typeof(LazyValue<HashSet<Type>>), "visited");
                Expression body = Expression.Constant(true);

                var fields = GetAllFields(runtimeType);
                foreach (var field in fields)
                {
                    var xField = Expression.Field(xParam, field);
                    var yField = Expression.Field(yParam, field);

                    Expression equalExpression;
                    if (IsPrimitiveType(field.FieldType))
                    {
                        equalExpression = Expression.Equal(xField, yField);
                    }
                    else
                    {
                        // Obtain the DeepEquals method from the correct GenericObjectComparer
                        var deepEqualsMethod = typeof(GenericDeepComparer<>).MakeGenericType(field.FieldType)
                            .GetMethod("DeepEquals", BindingFlags.NonPublic | BindingFlags.Static)
                            .MakeGenericMethod(field.FieldType);

                        equalExpression = Expression.Call(deepEqualsMethod, xField, yField, visitedParam);
                    }

                    body = Expression.AndAlso(body, equalExpression);
                }

                var comparer = Expression.Lambda<Func<T, T, LazyValue<HashSet<Type>>, bool>>(body, xParam, yParam, visitedParam).Compile();

                return comparer;
            }

            private static IEnumerable<FieldInfo> GetAllFields(Type type)
            {
                if (type == null) return Enumerable.Empty<FieldInfo>();
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                return type.GetFields(flags).Concat(GetAllFields(type.BaseType));
            }

            private static bool DeepEquals<TField>(TField x, TField y, LazyValue<HashSet<Type>> visited)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;

                var type = x.GetType();
                if (type != y.GetType()) return false;

                // Check if this type has already been visited -> TODO: This does not make sense, or does it? Shouldn't we check if the object has already been visited?
                if (visited.ObjIfExists?.Contains(type) ?? false) return true;
                visited.Obj.Add(type);

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var xValue = field.GetValue(x);
                    var yValue = field.GetValue(y);

                    if (IsPrimitiveType(field.FieldType))
                    {
                        if (!Equals(xValue, yValue)) return false;
                    }
                    // TODO Handle collections
                    else
                    {
                        if (!DeepEquals(xValue, yValue, visited)) return false;
                    }
                }

                return true;
            }

            private static bool IsPrimitiveType(Type type)
            {
                return type.IsPrimitive ||
                       type == typeof(string) ||
                       type == typeof(DateTime) ||
                       type == typeof(Decimal) ||
                       type == typeof(DateTimeOffset) ||
                       type == typeof(TimeSpan) ||
                       type == typeof(Guid) ||
                       type == typeof(Uri) ||
                       type.IsEnum ||
                       (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && IsPrimitiveType(Nullable.GetUnderlyingType(type))); // Handle Nullable types
            }

        }
    }

}
