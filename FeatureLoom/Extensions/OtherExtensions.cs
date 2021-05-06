using FeatureLoom.Serialization;
using System;

namespace FeatureLoom.Extensions
{
    public static class OtherExtensions
    {
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

        public static bool TrySetValue<T, V>(this T obj, string fieldOrPropertyName, V value) where T : class
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
    }
}