using System;
using System.Reflection;

namespace FeatureLoom.Extensions
{
    /// <summary>
    /// Member enumeration with a stable declaration order.
    /// On .NET Framework, GetFields/GetProperties return members in reflection-cache order, which changes
    /// when single members were looked up by name before (e.g. GetField("B") makes B come first).
    /// Modern runtimes return declaration order, so there the calls are passed through unchanged.
    /// </summary>
    internal static class ReflectionOrderExtensions
    {
        public static FieldInfo[] GetFieldsInDeclarationOrder(this Type type, BindingFlags flags)
        {
#if NETSTANDARD2_0
            return SortByDeclaration(type.GetFields(flags));
#else
            return type.GetFields(flags);
#endif
        }

        public static PropertyInfo[] GetPropertiesInDeclarationOrder(this Type type, BindingFlags flags)
        {
#if NETSTANDARD2_0
            return SortByDeclaration(type.GetProperties(flags));
#else
            return type.GetProperties(flags);
#endif
        }

#if NETSTANDARD2_0
        // Keeps the grouping by declaring type (derived before base) and orders by metadata token inside each group.
        private static T[] SortByDeclaration<T>(T[] members) where T : MemberInfo
        {
            if (members.Length < 2) return members;
            var keys = new (int depth, int token)[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                int depth = 0;
                for (Type t = members[i].DeclaringType; t != null; t = t.BaseType) depth++;
                keys[i] = (-depth, members[i].MetadataToken);
            }
            Array.Sort(keys, members);
            return members;
        }
#endif
    }
}
