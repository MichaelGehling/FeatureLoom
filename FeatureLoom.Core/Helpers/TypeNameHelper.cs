using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FeatureLoom.Extensions;
using System.Reflection;
using FeatureLoom.Logging;
using FeatureLoom.Helpers;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Helpers
{
    public class TypeNameHelper
    {
        public static TypeNameHelper Shared { get; } = new TypeNameHelper();

        public List<string> SupplementaryAssemblies { get; } = new List<string>();
        readonly private Dictionary<Assembly, Box<int>> checkedAssemblies = new();


        readonly private Dictionary<Type, string> typeToName = new Dictionary<Type, string>();
        readonly private FeatureLock typeToNameLock = null;

        readonly private Dictionary<string, Type> nameToType = new Dictionary<string, Type>();
        readonly private FeatureLock nameToTypeLock = null;

        
        public TypeNameHelper(bool threadSafe)
        {
            if (threadSafe)
            {
                typeToNameLock = new();
                nameToTypeLock = new();
            }
        }
        

        public TypeNameHelper()
        {
            typeToNameLock = new();
            nameToTypeLock = new();
        }

        /// <summary>
        /// Creates a simplified, human-readable type name string from a <see cref="Type"/> object.
        /// This format omits assembly qualifications, making it cleaner for display or serialization.
        /// The result can be converted back to a <see cref="Type"/> using <see cref="GetTypeFromSimplifiedName"/>.
        /// <example>
        /// <code>
        /// typeof(int) -> "System.Int32"
        /// typeof(List&lt;string&gt;) -> "System.Collections.Generic.List&lt;System.String&gt;"
        /// typeof(Dictionary&lt;string, object&gt;[]) -> "System.Collections.Generic.Dictionary&lt;System.String, System.Object&gt;[]"
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="type">The type to get the simplified name for.</param>
        /// <returns>A simplified type name string.</returns>
        public string GetSimplifiedTypeName(Type type)
        {
            if (typeToNameLock == null)
            {
                if (typeToName.TryGetValue(type, out string typeName)) return typeName;
                return LockedGetSimplifiedTypeName(type);
            }

            using (var lockHandle = typeToNameLock.LockReadOnly())
            {
                if (typeToName.TryGetValue(type, out string typeName)) return typeName;

                lockHandle.UpgradeToWriteMode();

                return LockedGetSimplifiedTypeName(type);
            }
        }

        /// <summary>
        /// Resolves a <see cref="Type"/> from its simplified type name string, as created by <see cref="GetSimplifiedTypeName"/>.
        /// It searches all currently loaded assemblies and any assemblies specified in <see cref="SupplementaryAssemblies"/>.
        /// <example>
        /// <code>
        /// "System.Int32" -> typeof(int)
        /// "System.Collections.Generic.List&lt;System.String&gt;" -> typeof(List&lt;string&gt;)
        /// "System.Collections.Generic.Dictionary&lt;System.String, System.Object&gt;[]" -> typeof(Dictionary&lt;string, object&gt;[])
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="typeName">The simplified type name string.</param>
        /// <returns>The resolved <see cref="Type"/> object, or null if the type could not be found.</returns>
        public Type GetTypeFromSimplifiedName(string typeName)
        {
            if (nameToTypeLock == null)
            {
                if (nameToType.TryGetValue(typeName, out Type type)) return type;
                return LockedGetTypeFromSimplifiedName(typeName);
            }

            using (var lockHandle = nameToTypeLock.LockReadOnly())
            {
                if (nameToType.TryGetValue(typeName, out Type type)) return type;

                lockHandle.UpgradeToWriteMode();

                return LockedGetTypeFromSimplifiedName(typeName);
            }
        }

        private string LockedGetSimplifiedTypeName(Type type)
        {
            string typeName;
            if (type.IsArray)
            {
                typeName = LockedGetSimplifiedTypeName(type.GetElementType()) + "[]";
            }
            else if (type.IsGenericType)
            {
                var genericTypeDefName = type.GetGenericTypeDefinition().FullName;
                var name = genericTypeDefName.Substring(0, genericTypeDefName.IndexOf('`'));
                var args = type.GetGenericArguments().Select(LockedGetSimplifiedTypeName);

                typeName = name + "<" + string.Join(", ", args) + ">";
            }
            else
            {
                typeName = type.FullName;
            }
            typeToName[type] = typeName;
            return typeName;
        }

        private Type LockedGetTypeFromSimplifiedName(string typeName)
        {
            Type type;
            if (typeName.EndsWith("[]"))
            {
                // It's an array type
                var elementType = LockedGetTypeFromSimplifiedName(typeName.Substring(0, typeName.Length - 2));
                type = elementType?.MakeArrayType();
            }
            else if (typeName.Contains("<") && typeName.Contains(">"))
            {
                // It's a generic type                
                int paramListStart = typeName.IndexOf('<') + 1;
                int paramListEnd = typeName.LastIndexOf('>');
                if (paramListEnd <= paramListStart) return null;
                int paramListLength = paramListEnd - paramListStart;
                var genericTypeArgs = typeName.Substring(paramListStart, paramListLength)
                    .Split(new[] { ", " }, StringSplitOptions.None)
                    .Select(LockedGetTypeFromSimplifiedName)
                    .ToArray();

                if (genericTypeArgs.Any(t => t == null)) return null;

                var genericTypeDefName = typeName.Substring(0, typeName.IndexOf('<')) + '`' + genericTypeArgs.Length;

                // Get generic type definition from all loaded assemblies
                var genericTypeDef = GetTypeFromAssemblies(genericTypeDefName, AppDomain.CurrentDomain.GetAssemblies().OrderByDescending(ass => checkedAssemblies.TryGetValue(ass, out var count) ? count.value : 0));
                if (genericTypeDef == null) genericTypeDef = LoadAndGetTypeFromSupplementaryAssemblies(genericTypeDefName);
                
                if (genericTypeDef != null)
                {
                    type = genericTypeDef.MakeGenericType(genericTypeArgs);
                }
                else
                {
                    type = null;
                }
            }
            else
            {
                // It's a simple type
                type = GetTypeFromAssemblies(typeName, AppDomain.CurrentDomain.GetAssemblies().OrderByDescending(ass => checkedAssemblies.TryGetValue(ass, out var count) ? count.value : 0));
                if (type == null) type = LoadAndGetTypeFromSupplementaryAssemblies(typeName);
            }

            if (type == null)
            {
                // Could not find type as Simplified, try normal type naming
                type = Type.GetType(typeName, false, true);
            }

            if (type != null) nameToType[typeName] = type;
            return type;
        }

        private Type GetTypeFromAssembly(string typeName, Assembly assembly)
        {
            Type type = assembly.GetType(typeName, throwOnError: false);

            if (!checkedAssemblies.TryGetValue(assembly, out var count))
            {
                count = new Box<int>(0);
                checkedAssemblies[assembly] = count;
            }
            if (type != null) count.value++;

            return type;
        }

        private Type GetTypeFromAssemblies(string typeName, IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var type = GetTypeFromAssembly(typeName, assembly);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private Type LoadAndGetTypeFromSupplementaryAssemblies(string typeName)
        {
            foreach (var assemblyPath in SupplementaryAssemblies)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    // Skip this assembly if it has been checked before
                    if (checkedAssemblies.ContainsKey(assembly)) continue;

                    var type = GetTypeFromAssembly(typeName, assembly);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build($"Could not load assembly '{assemblyPath}'.", e);
                }
            }

            return null;
        }

        readonly private Dictionary<Type, string> typeToFullName = new Dictionary<Type, string>();
        readonly private Dictionary<Type, string> typeToAssemblyQualifiedName = new Dictionary<Type, string>();

        /// <summary>
        /// Creates the plain CLR full name of a type, without any assembly information.
        /// For generic types this keeps the CLR notation, e.g.
        /// "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]]".
        /// <example>
        /// <code>
        /// typeof(int) -> "System.Int32"
        /// typeof(Customer) -> "MyApp.Models.Customer"
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="type">The type to get the full name for.</param>
        /// <returns>The full name, or the type's Name if no full name is available.</returns>
        public string GetFullTypeName(Type type)
        {
            if (type == null) return null;

            if (typeToNameLock == null) return LockedGetFullTypeName(type);

            using (var lockHandle = typeToNameLock.LockReadOnly())
            {
                if (typeToFullName.TryGetValue(type, out string typeName)) return typeName;

                lockHandle.UpgradeToWriteMode();

                return LockedGetFullTypeName(type);
            }
        }

        private string LockedGetFullTypeName(Type type)
        {
            if (typeToFullName.TryGetValue(type, out string typeName)) return typeName;

            // FullName is null for open generic parameters, so fall back to the plain name.
            typeName = type.FullName ?? type.Name;
            typeToFullName[type] = typeName;
            return typeName;
        }

        /// <summary>
        /// Creates the assembly qualified name of a type using the short assembly name, which is
        /// the format Newtonsoft.Json writes when TypeNameHandling is enabled.
        /// Version, culture and public key token are stripped, also from generic arguments.
        /// <example>
        /// <code>
        /// typeof(Customer) -> "MyApp.Models.Customer, MyApp"
        /// typeof(List&lt;string&gt;) -> "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib"
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="type">The type to get the assembly qualified name for.</param>
        /// <returns>The assembly qualified name using the short assembly name.</returns>
        public string GetAssemblyQualifiedTypeName(Type type)
        {
            if (type == null) return null;

            if (typeToNameLock == null) return LockedGetAssemblyQualifiedTypeName(type);

            using (var lockHandle = typeToNameLock.LockReadOnly())
            {
                if (typeToAssemblyQualifiedName.TryGetValue(type, out string typeName)) return typeName;

                lockHandle.UpgradeToWriteMode();

                return LockedGetAssemblyQualifiedTypeName(type);
            }
        }

        private string LockedGetAssemblyQualifiedTypeName(Type type)
        {
            if (typeToAssemblyQualifiedName.TryGetValue(type, out string typeName)) return typeName;

            typeName = BuildAssemblyQualifiedTypeName(type);
            typeToAssemblyQualifiedName[type] = typeName;
            return typeName;
        }

        /// <summary>
        /// Builds "Namespace.Type, Assembly" and recurses into generic arguments, so that the
        /// nested generic arguments are shortened the same way Newtonsoft.Json does it.
        /// </summary>
        private static string BuildAssemblyQualifiedTypeName(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;

            if (type.IsArray)
            {
                // Arrays keep their suffix, but the element type has to be shortened as well.
                var elementName = BuildAssemblyQualifiedTypeName(type.GetElementType());
                int elementAssemblySeparator = elementName.LastIndexOf(", ", StringComparison.Ordinal);
                if (elementAssemblySeparator > 0) elementName = elementName.Substring(0, elementAssemblySeparator);

                int rank = type.GetArrayRank();
                string suffix = rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]";
                return elementName + suffix + ", " + assemblyName;
            }

            if (!type.IsGenericType || type.IsGenericTypeDefinition)
            {
                return (type.FullName ?? type.Name) + ", " + assemblyName;
            }

            var genericDefName = type.GetGenericTypeDefinition().FullName;
            var builder = new StringBuilder(genericDefName).Append('[');
            var args = type.GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append('[').Append(BuildAssemblyQualifiedTypeName(args[i])).Append(']');
            }
            builder.Append(']').Append(", ").Append(assemblyName);
            return builder.ToString();
        }

    }
}

