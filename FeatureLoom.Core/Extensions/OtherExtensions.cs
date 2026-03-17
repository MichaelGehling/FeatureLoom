using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Extensions
{
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Attempts to set the value of a specified field or property on an object using reflection.
        /// </summary>
        /// <remarks>This method searches for the specified field or property in the object's type and its
        /// base types, including non-public members. If the value is not directly assignable to the member's type but
        /// implements IConvertible, an attempt is made to convert it. The method does not throw an exception if the
        /// member is not found or the assignment fails; instead, it returns false.</remarks>
        /// <typeparam name="T">The type of the object on which the field or property is being set.</typeparam>
        /// <typeparam name="V">The type of the value being assigned to the field or property.</typeparam>
        /// <param name="obj">The instance of the object on which the field or property will be set. Cannot be null.</param>
        /// <param name="fieldOrPropertyName">The name of the field or property to set on the object. This search is case-sensitive and includes both
        /// public and non-public members.</param>
        /// <param name="value">The value to assign to the specified field or property. The value must be compatible with the member's type
        /// or convertible to it.</param>
        /// <returns>true if the value was successfully set; otherwise, false.</returns>
        public static bool TrySetValueUsingReflection<T, V>(this T obj, string fieldOrPropertyName, V value) where T : class
        {
            Type type = obj.GetType();

            var property = type.GetProperty(fieldOrPropertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                Type baseType = type.BaseType;
                while (baseType != null && property == null)
                {
                    property = baseType.GetProperty(fieldOrPropertyName, BindingFlags.NonPublic | BindingFlags.Instance);
                    baseType = baseType.BaseType;
                }
            }
            if (property != null)
            {
                if (!property.CanWrite) return false;

                if (property.PropertyType.IsAssignableFrom(typeof(V)))
                {
                    property.SetValue(obj, value);
                    return true;
                }
                else if (value is IConvertible convertible && convertible.TryConvertTo(property.PropertyType, out object converted))
                {
                    property.SetValue(obj, converted);
                    return true;
                }
                else return false;
            }

            var field = type.GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Type baseType = type.BaseType;
                while (baseType != null && field == null)
                {
                    field = baseType.GetField(fieldOrPropertyName, BindingFlags.NonPublic | BindingFlags.Instance);
                    baseType = baseType.BaseType;
                }
            }
            if (field != null)
            {
                if (field.FieldType.IsAssignableFrom(typeof(V)))
                {
                    field.SetValue(obj, value);
                    return true;
                }
                else if (value is IConvertible convertible && convertible.TryConvertTo(field.FieldType, out object converted))
                {
                    field.SetValue(obj, converted);
                    return true;
                }
                else return false;
            }

            return false;
        }

        /// <summary>
        /// Attempts to retrieve the value of a specified field or property from an object using reflection.
        /// </summary>
        /// <remarks>This method searches for both public and non-public instance fields and properties,
        /// including those declared in base types. If the value cannot be directly assigned to the specified type, an
        /// attempt is made to convert it using IConvertible if applicable.</remarks>
        /// <typeparam name="T">The type of the object from which the field or property value is being retrieved.</typeparam>
        /// <typeparam name="V">The type of the value that is expected to be retrieved from the field or property.</typeparam>
        /// <param name="obj">The object instance from which to retrieve the field or property value.</param>
        /// <param name="fieldOrPropertyName">The name of the field or property whose value is to be retrieved.</param>
        /// <param name="value">When this method returns, contains the value of the specified field or property, or the default value of
        /// type V if the retrieval fails.</param>
        /// <returns>true if the value was successfully retrieved and converted to the specified type; otherwise, false.</returns>
        public static bool TryGetValueUsingReflection<T, V>(this T obj, string fieldOrPropertyName, out V value) where T : class
        {
            value = default;
            Type type = obj.GetType();

            var property = type.GetProperty(fieldOrPropertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                Type baseType = type.BaseType;
                while (baseType != null && property == null)
                {
                    property = baseType.GetProperty(fieldOrPropertyName, BindingFlags.NonPublic | BindingFlags.Instance);
                    baseType = baseType.BaseType;
                }
            }
            if (property != null)
            {
                if (!property.CanRead) return false;

                var propertyValue = property.GetValue(obj);
                if (propertyValue is V typedValue)
                {
                    value = typedValue;
                    return true;
                }
                else if (propertyValue is IConvertible convertible && convertible.TryConvertTo(out value))
                {
                    return true;
                }
                return false;
            }

            var field = type.GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Type baseType = type.BaseType;
                while (baseType != null && field == null)
                {
                    field = baseType.GetField(fieldOrPropertyName, BindingFlags.NonPublic | BindingFlags.Instance);
                    baseType = baseType.BaseType;
                }
            }
            if (field != null)
            {
                var fieldValue = field.GetValue(obj);
                if (fieldValue is V typedFieldValue)
                {
                    value = typedFieldValue;
                    return true;
                }
                else if (fieldValue is IConvertible convertible && convertible.TryConvertTo(out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static MethodInfo FindGenericMethod(object obj, string methodName, Type[] typeArguments, out object invocationTarget)
        {
            Type targetType;
            bool staticOnly;

            if (obj is Type type)
            {
                targetType = type;
                staticOnly = true;
                invocationTarget = null;
            }
            else
            {
                targetType = obj.GetType();
                staticOnly = false;
                invocationTarget = obj;
            }

            var method = targetType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m =>
                    m.Name == methodName &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == typeArguments.Length &&
                    (!staticOnly || m.IsStatic));

            if (method != null && method.IsStatic) invocationTarget = null;
            return method;
        }

        /// <summary>
        /// Invokes a generic method on the specified object or type using the provided method name, type arguments, and
        /// argument values.
        /// </summary>
        /// <param name="obj">The object instance to invoke on, or a <see cref="Type"/> to invoke a static method on.</param>
        /// <param name="methodName">The name of the generic method to invoke.</param>
        /// <param name="typeArguments">The type arguments to use for the generic method.</param>
        /// <param name="argumentValues">The argument values to pass to the method.</param>
        /// <returns>The result of the method invocation cast to the specified return type.</returns>
        public static void InvokeGenericMethod(this object obj, string methodName, Type[] typeArguments, params object[] argumentValues)
        {
            var method = FindGenericMethod(obj, methodName, typeArguments, out object invocationTarget);
            if (method == null) throw new NotSupportedException();

            var genericMethod = method.MakeGenericMethod(typeArguments);
            genericMethod.Invoke(invocationTarget, argumentValues);
        }

        /// <summary>
        /// Invokes a generic method with the specified name and type arguments on the given object or type, passing the
        /// provided argument values, and returns the result cast to the specified return type.
        /// </summary>
        /// <param name="obj">The object instance to invoke on, or a <see cref="Type"/> to invoke a static method on.</param>
        /// <param name="methodName">The name of the generic method to invoke.</param>
        /// <param name="typeArguments">The type arguments to use for the generic method.</param>
        /// <param name="argumentValues">The argument values to pass to the method.</param>
        /// <returns>The result of the method invocation cast to the specified return type.</returns>
        public static RET InvokeGenericMethod<RET>(this object obj, string methodName, Type[] typeArguments, params object[] argumentValues)
        {
            var method = FindGenericMethod(obj, methodName, typeArguments, out object invocationTarget);
            if (method == null) throw new NotSupportedException();

            var genericMethod = method.MakeGenericMethod(typeArguments);
            return (RET)genericMethod.Invoke(invocationTarget, argumentValues);
        }
    }

    public static class OtherExtensions
    {
        /// <summary>
        /// Attempts to cast the specified object to the specified reference type, returning null if the cast is not possible.
        /// </summary>
        /// <remarks>Unlike 'as casting' it can easily be used in a fluent pattern</remarks>
        /// <typeparam name="T">The reference type to which to cast the object.</typeparam>
        /// <param name="obj">The object to cast. Can be null.</param>
        /// <returns>The object cast to type T if the cast is successful; otherwise, null.</returns>
        public static T As<T>(this object obj) where T : class => obj as T;

        /// <summary>
        /// Gets the target object referenced by the specified weak reference, or returns a default value if the target
        /// is no longer available.
        /// </summary>
        /// <remarks>Use this method to safely access the target of a weak reference without risking a
        /// null reference exception. This is particularly useful when the referenced object may have been garbage
        /// collected.</remarks>
        /// <typeparam name="T">The type of the object referenced by the weak reference.</typeparam>
        /// <param name="weakRef">The weak reference from which to retrieve the target object. This parameter cannot be null.</param>
        /// <param name="defaultObj">The value to return if the target object is not available. This parameter can be null.</param>
        /// <returns>The target object if it is available; otherwise, the specified default value.</returns>
        public static T GetTargetOrDefault<T>(this WeakReference<T> weakRef, T defaultObj = default) where T : class
        {
            if (weakRef.TryGetTarget(out T target)) return target;
            else return defaultObj;
        }

        /// <summary>
        /// Can be used as an alternative for the ternary operator (condition?a:b) if the result has to be used in a fluent pattern.
        /// Be aware that both expressions for the parameters will be executed in contrast to the ternary operator where only one expression is executed.
        /// So avoid usage if for any of the parameters an expensive expression is used (e.g. create object). 
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="decision">The condition to evaluate.</param>
        /// <param name="whenTrue">The value to return if the condition is true.</param>
        /// <param name="whenFalse">The value to return if the condition is false.</param>
        /// <returns>The value corresponding to the evaluated condition.</returns>
        public static T IfTrue<T>(this bool decision, T whenTrue, T whenFalse)
        {
            return decision ? whenTrue : whenFalse;
        }

        /// <summary>
        /// Retrieves the innermost exception from the specified exception, or the exception itself if there are no
        /// inner exceptions.
        /// </summary>
        /// <remarks>This method is useful for unwrapping exceptions to find the root cause of an error,
        /// especially in cases where exceptions are wrapped in multiple layers.</remarks>
        /// <param name="e">The exception from which to retrieve the innermost exception. This parameter cannot be null.</param>
        /// <returns>The innermost exception, or the original exception if no inner exceptions exist.</returns>
        public static Exception InnerOrSelf(this Exception e)
        {
            while (e.InnerException != null) e = e.InnerException.InnerOrSelf();
            return e;
        }

        /// <summary>
        /// Attempts to convert the specified object to the specified type, returning a value that indicates whether the
        /// conversion was successful.
        /// </summary>
        /// <remarks>This method attempts to convert the input object to the specified type using several
        /// strategies, including direct type conversion and json deserialization from string representations. If the
        /// conversion cannot be performed, the method returns false and sets the output parameter to the default value
        /// of the target type.</remarks>
        /// <typeparam name="T">The type of the object to convert. This type must implement the IConvertible interface.</typeparam>
        /// <param name="obj">The object to convert. Must be of a type that implements IConvertible.</param>
        /// <param name="type">The type to which the object is to be converted.</param>
        /// <param name="converted">When this method returns, contains the converted value if the conversion succeeded; otherwise,
        /// the default value for the target type.</param>
        /// <param name="provider">An optional format provider that supplies culture-specific formatting information. If null, the current
        /// culture is used.</param>
        /// <returns>true if the conversion succeeded; otherwise, false.</returns>
        public static bool TryConvertTo<T>(this T obj, Type type, out object converted, IFormatProvider provider = null) where T : IConvertible
        {     
            try
            {
                if (obj is TextSegment segment)
                {
                    if (segment.TryToType(type, out converted, provider)) return true;
                    return JsonHelper.DefaultDeserializer.TryDeserialize(segment.ToString(), out converted);                    
                }
                else if (obj is string str)
                {
                    if (new TextSegment(str).TryToType(type, out converted, provider)) return true;
                    return JsonHelper.DefaultDeserializer.TryDeserialize(str, out converted);
                }

                converted = Convert.ChangeType(obj, type, provider);
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }            
        }

        /// <summary>
        /// Attempts to convert the specified object to the specified type, returning a value that indicates whether the
        /// conversion was successful.
        /// </summary>
        /// <remarks>This method attempts to convert the input object to the specified type using several
        /// strategies, including direct type conversion and json deserialization from string representations. If the
        /// conversion cannot be performed, the method returns false and sets the output parameter to the default value
        /// of the target type.</remarks>
        /// <typeparam name="TIN">The type of the object to convert. This type must implement the IConvertible interface.</typeparam>
        /// <typeparam name="TOUT">The type to which the object is to be converted.</typeparam>
        /// <param name="obj">The object to convert. Must be of a type that implements IConvertible.</param>
        /// <param name="converted">When this method returns, contains the converted value of type TOUT if the conversion succeeded; otherwise,
        /// the default value for type TOUT.</param>
        /// <param name="provider">An optional format provider that supplies culture-specific formatting information. If null, the current
        /// culture is used.</param>
        /// <returns>true if the conversion succeeded; otherwise, false.</returns>
        public static bool TryConvertTo<TIN, TOUT>(this TIN obj, out TOUT converted, IFormatProvider provider = null) 
            where TIN : IConvertible 
        {

            // Add other conversions to avoid boxing through Convert.ChangeType for common types (e.g. int, double, bool)
            try
            {
                if (obj is TextSegment segment)
                {
                    if (segment.TryToType(out converted, provider)) return true;
                    return JsonHelper.DefaultDeserializer.TryDeserialize(segment.ToString(), out converted);
                }
                else if (obj is string str)
                {
                    if (new TextSegment(str).TryToType(out converted, provider)) return true;
                    return JsonHelper.DefaultDeserializer.TryDeserialize(str, out converted);
                }
                converted = (TOUT)Convert.ChangeType(obj, typeof(TOUT), provider);
                return true;
            }
            catch
            {
                converted = default;
                return false;
            }

        }

        /// <summary>
        /// Tells if a character is an ASCII digit (0-9). This is a more efficient alternative to char.IsDigit if only ASCII digits are relevant, as it avoids the overhead of checking for Unicode digit categories.
        /// </summary>
        /// <param name="c">The character to check.</param>
        /// <returns>true if the character is an ASCII digit; otherwise, false.</returns>
        public static bool IsAsciiDigit(this char c) => c >= '0' && c <= '9';

        /// <summary>
        /// Asynchronously waits until the specified feature lock is released before allowing the operation to proceed.
        /// </summary>
        /// <remarks>Use this method to ensure that code execution does not continue until the specified
        /// feature lock has been released by its current owner. This is useful for coordinating access to features that
        /// require exclusive use.</remarks>
        /// <param name="featureLock">The feature lock to wait for. This parameter must not be null and should represent a lock that may currently
        /// be held.</param>
        /// <returns>A task that represents the asynchronous wait operation. The task completes when the feature lock is no longer held.</returns>
        public static async Task WaitForExited(this FeatureLock featureLock)
        {
            if (!featureLock.IsLocked) return;

            using (await featureLock.LockAsync(true).ConfiguredAwait()) 
            {
                // Just lock with priority so we know when the lock was exited by previous owner 
            };
        }

        /// <summary>
        /// Creates a task that completes when the specified cancellation token is canceled.
        /// </summary>
        /// <remarks>This method is useful for awaiting cancellation in asynchronous operations, allowing
        /// for graceful termination of tasks.</remarks>
        /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
        /// <returns>A task that completes when the cancellation token is canceled.</returns>
        public static Task AwaitCancellation(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();
            cancellationToken.Register(() => tcs.TrySetResult(null));
            return tcs.Task;
        }

    }
}