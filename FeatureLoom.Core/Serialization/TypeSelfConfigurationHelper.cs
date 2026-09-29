using FeatureLoom.Helpers;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace FeatureLoom.Serialization
{
    /// <summary>
    /// Finds and validates the methods marked with <see cref="JsonTypeConfigurationAttribute"/>.
    /// The result per type is cached process wide, because it only depends on the type itself.
    /// </summary>
    internal static class TypeSelfConfigurationHelper
    {
        private sealed class Methods
        {
            public MethodInfo writeMethod;
            public MethodInfo readMethod;
        }

        private static readonly ConcurrentDictionary<Type, Methods> cache = new();

        /// <summary>Returns the write configuration method of <paramref name="type"/> or <see langword="null"/>.</summary>
        public static MethodInfo GetWriteMethod(Type type) => GetMethods(type)?.writeMethod;

        /// <summary>Returns the read configuration method of <paramref name="type"/> or <see langword="null"/>.</summary>
        public static MethodInfo GetReadMethod(Type type) => GetMethods(type)?.readMethod;

        private static Methods GetMethods(Type type)
        {
            // Only classes and structs can declare the method. Open generic types can never be the
            // runtime type, so they are excluded as well.
            if (type == null || type.IsInterface || type.IsPrimitive || type.IsGenericTypeDefinition || type.IsArray) return null;
            return cache.GetOrAdd(type, FindMethods);
        }

        private static Methods FindMethods(Type type)
        {
            Type writeParamType = typeof(JsonSerializer.TypeWriteSettings<>).MakeGenericType(type);
            Type readParamType = typeof(JsonDeserializer.TypeSettings<>).MakeGenericType(type);

            Methods result = null;
            // DeclaredOnly: like settings entries, the configuration is not inherited by derived types.
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (!method.IsDefined(typeof(JsonTypeConfigurationAttribute), false)) continue;

                var parameters = method.GetParameters();
                Type paramType = parameters.Length == 1 ? parameters[0].ParameterType : null;
                bool isWrite = paramType == writeParamType;
                bool isRead = paramType == readParamType;
                if (method.ReturnType != typeof(void) || (!isWrite && !isRead))
                {
                    throw new Exception($"Method {method.Name} of type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)} is marked with {nameof(JsonTypeConfigurationAttribute)}, " +
                                        $"but has an invalid signature. Expected 'static void {method.Name}(JsonSerializer.TypeWriteSettings<T>)' or " +
                                        $"'static void {method.Name}(JsonDeserializer.TypeSettings<T>)' where T is the declaring type.");
                }

                result ??= new Methods();
                if (isWrite)
                {
                    if (result.writeMethod != null) throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)} has more than one write configuration method marked with {nameof(JsonTypeConfigurationAttribute)}.");
                    result.writeMethod = method;
                }
                else
                {
                    if (result.readMethod != null) throw new Exception($"Type {TypeNameHelper.Shared.GetSimplifiedTypeName(type)} has more than one read configuration method marked with {nameof(JsonTypeConfigurationAttribute)}.");
                    result.readMethod = method;
                }
            }
            return result;
        }
    }
}
