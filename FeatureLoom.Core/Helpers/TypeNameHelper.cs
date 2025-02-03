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
    /// <summary>
    /// Not supported are 
    /// - open constructed types (typeof(List<>)) 
    /// </summary>
    public static class TypeNameHelper
    {
        public static List<string> SupplementaryAssemblies { get; } = new List<string>();
        private static Dictionary<Assembly, Box<int>> checkedAssemblies = new();


        private static Dictionary<Type, string> typeToName = new Dictionary<Type, string>();
        private static FeatureLock typeToNameLock = new FeatureLock();

        private static Dictionary<string, Type> nameToType = new Dictionary<string, Type>();
        private static FeatureLock nameToTypeLock = new FeatureLock();

        public static string GetSimplifiedTypeName(Type type)
        {
            using (var lockHandle = typeToNameLock.LockReadOnly())
            {
                if (typeToName.TryGetValue(type, out string typeName)) return typeName;

                lockHandle.UpgradeToWriteMode();

                return LockedGetSimplifiedTypeName(type);
            }
        }

        public static Type GetTypeFromSimplifiedName(string typeName)
        {
            using (var lockHandle = nameToTypeLock.LockReadOnly())
            {
                if (nameToType.TryGetValue(typeName, out Type type)) return type;

                lockHandle.UpgradeToWriteMode();

                return LockedGetTypeFromSimplifiedName(typeName);
            }
        }

        private static string LockedGetSimplifiedTypeName(Type type)
        {
            string typeName;
            if (type.IsArray)
            {
                typeName = LockedGetSimplifiedTypeName(type.GetElementType()) + "[]";
            }
            else if (type.IsGenericType)
            {
                var name = type.FullName.Substring(0, type.FullName.IndexOf('`'));
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

        private static Type LockedGetTypeFromSimplifiedName(string typeName)
        {
            Type type;
            if (typeName.EndsWith("[]"))
            {
                // It's an array type
                type = LockedGetTypeFromSimplifiedName(typeName.Substring(0, typeName.Length - 2)).MakeArrayType();
            }
            else if (typeName.Contains("<") && typeName.Contains(">"))
            {
                // It's a generic type                
                int paramListStart = typeName.IndexOf('<')+1;
                int paramListEnd = typeName.LastIndexOf('>');
                int paramListLength = paramListEnd - paramListStart;
                var genericTypeArgs = typeName.Substring(paramListStart, paramListLength)
                    .Split(new[] { ", " }, StringSplitOptions.None)
                    .Select(LockedGetTypeFromSimplifiedName)
                    .ToArray();

                var genericTypeDefName = typeName.Substring(0, typeName.IndexOf('<')) + '`' + genericTypeArgs.Length;

                // Get generic type definition from all loaded assemblies
                var genericTypeDef = GetTypeFromAssemblies(genericTypeDefName, AppDomain.CurrentDomain.GetAssemblies().OrderByDescending(ass=> checkedAssemblies.TryGetValue(ass, out var count) ? count.value : 0));
                if (genericTypeDef == null) genericTypeDef = LoadAndGetTypeFromSupplementaryAssemblies(genericTypeDefName);
                if (genericTypeDef == null) OptLog.ERROR()?.Build($"Could not find type '{genericTypeDefName}'.");
                type = genericTypeDef.MakeGenericType(genericTypeArgs);
            }
            else
            {
                // It's a simple type
                type = GetTypeFromAssemblies(typeName, AppDomain.CurrentDomain.GetAssemblies().OrderByDescending(ass => checkedAssemblies.TryGetValue(ass, out var count) ? count.value : 0));
                if (type == null) type = LoadAndGetTypeFromSupplementaryAssemblies(typeName);
                if (type == null) OptLog.ERROR()?.Build($"Could not find type '{typeName}'.");
            }

            if (type != null) nameToType[typeName] = type;
            return type;
        }

        private static Type GetTypeFromAssembly(string typeName, Assembly assembly)
        {
            Type type = assembly.GetTypes().FirstOrDefault(t => t.FullName == typeName);

            if (checkedAssemblies.TryGetValue(assembly, out var count)) count.value += type == null ? 0 : 1;
            else checkedAssemblies.Add(assembly, type == null ? 0 : 1);

            return type;
        }

        private static Type GetTypeFromAssemblies(string typeName, IEnumerable<Assembly> assemblies)
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

        private static Type LoadAndGetTypeFromSupplementaryAssemblies(string typeName)
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

    }
}

