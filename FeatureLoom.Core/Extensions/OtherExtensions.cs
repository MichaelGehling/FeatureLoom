using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FeatureLoom.Extensions
{
    public static class OtherExtensions
    {
        public static T As<T>(this object obj) where T : class => obj as T;

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
        public static T IfTrue<T>(this bool decision, T whenTrue, T whenFalse)
        {
            return decision ? whenTrue : whenFalse;
        }

        public static Exception InnerOrSelf(this Exception e)
        {
            return e.InnerException ?? e;
        }

        public static bool TryClone<T>(this T obj, out T clone)
        {            
            clone = default;
            if (obj == null) return true;

            Type type = obj.GetType();

            if (type.IsPrimitive)
            {
                clone = obj;
                return true;
            }

            if (obj == null)
            {
                clone = default;
                return true;
            }

            if (obj is ICollection)
            {
                throw new NotImplementedException();
            }
            else
            {

                bool constructorFound = false;
                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var c in constructors)
                {
                    if (c.GetParameters().Length == 0)
                    {
                        clone = (T)c.Invoke(Array.Empty<object>());
                        constructorFound = true;
                        break;
                    }
                }

                if (!constructorFound)
                {
                    clone = default;
                    return false;
                }


                List<FieldInfo> fields;
                fields = new List<FieldInfo>();
                fields.AddRange(type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
                Type t = type.BaseType;
                while (t != null)
                {

                    fields.AddRange(t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(baseField => !fields.Any(field => field.Name == baseField.Name)));
                    t = t.BaseType;
                }

                foreach (FieldInfo field in fields)
                {
                    object value = field.GetValue(obj);
                    Type fieldType = field.FieldType;
                    if (!fieldType.IsPrimitive && fieldType != typeof(string))
                    {
                        TryClone(value, out value);
                    }
                    field.SetValue(clone, value);
                }

                return true;
            }
        }

        public static bool TrySetValueByName<T, V>(this T obj, string fieldOrPropertyName, V value) where T : class
        {
            var property = obj.GetType().GetProperty(fieldOrPropertyName);
            if (property != null)
            {
                if (!property.CanWrite) return false;

                if (property.PropertyType.IsAssignableFrom(typeof(V)))
                {
                    property.SetValue(obj, value);
                }
                else if (value is IConvertible convertible && convertible.TryConvertTo(property.PropertyType, out object converted))
                {
                    property.SetValue(obj, converted);
                }
                else return false;
            }
            else
            {
                var field = obj.GetType().GetField(fieldOrPropertyName);
                if (field == null) return false;

                if (field.FieldType.IsAssignableFrom(typeof(V)))
                {
                    field.SetValue(obj, value);
                }
                else if (value is IConvertible convertible && convertible.TryConvertTo(field.FieldType, out object converted))
                {
                    field.SetValue(obj, converted);
                }
                else return false;
            }

            return true;
        }

        public static bool TryConvertTo<T>(this T obj, Type type, out object converted) where T : IConvertible
        {
            converted = null;
            try
            {
                converted = Convert.ChangeType(obj, type);
            }
            catch
            {
                try
                {
                    if (obj is string str)
                    {
                        converted = Json.DeserializeFromJson(str, type);
                    }
                }
                catch
                {
                }
            }

            return converted != null;
        }

        public static bool TryConvertTo<TIN, TOUT>(this TIN obj, out TOUT converted) 
            where TIN : IConvertible 
        {
            if (obj.TryConvertTo(typeof(TOUT), out object convertedObj))
            {
                converted = (TOUT) convertedObj;
                return true;
            }
            else
            {
                converted = default;
                return false;
            }

        }

        public static bool IsAsciiDigit(this char c) => c >= '0' && c <= '9';

        public static async Task WaitForExited(this FeatureLock featureLock)
        {
            if (!featureLock.IsLocked) return;

            using (await featureLock.LockAsync(true)) 
            {
                // Just lock with priority so we know when the lock was exited by previous owner 
            };
        }
        
        public static void InvokeGenericMethod(this object obj, string methodName, Type[] typeArguments, params object[] argumentValues)
        {
            var createMethod = obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == methodName && method.GetGenericArguments().Length == typeArguments.Length);
            if (createMethod == null) throw new NotSupportedException();
            var genericCreateMethod = createMethod.MakeGenericMethod(typeArguments);
            genericCreateMethod.Invoke(obj, argumentValues);
        }

    }
}