using FeatureLoom.Helpers;
using System;
using System.Linq;

namespace FeatureLoom.Extensions
{
    public static class TypeExtensions
    {
        public static string GetSimplifiedTypeName(this Type type) => TypeNameHelper.GetSimplifiedTypeName(type);

        public static bool IsNullable(this Type type) => Nullable.GetUnderlyingType(type) != null;

        public static bool ImplementsGenericInterface(this Type typeToCheck, Type genericInterfaceType)
        {
            return typeToCheck.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == genericInterfaceType);
        }

        public static Type GetFirstTypeParamOfGenericInterface(this Type typeToCheck, Type genericInterfaceType)
        {
            foreach (var type in typeToCheck.GetInterfaces())
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
                {
                    return type.GetGenericArguments()[0];
                }
            }
            return null;
        }

        public static Type[] GetAllTypeParamsOfGenericInterface(this Type typeToCheck, Type genericInterfaceType)
        {
            foreach (var type in typeToCheck.GetInterfaces())
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
                {
                    return type.GetGenericArguments();
                }
            }
            return null;
        }

        public static bool TryGetTypeParamsOfGenericInterface(this Type typeToCheck, Type genericInterfaceType, out Type param1)
        {
            param1 = null;
            foreach (var type in typeToCheck.GetInterfaces())
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
                {
                    return type.GetGenericArguments().TryElementsOut(out param1);
                }
            }
            return false;
        }

        public static bool TryGetTypeParamsOfGenericInterface(this Type typeToCheck, Type genericInterfaceType, out Type param1, out Type param2)
        {
            param1 = null; 
            param2 = null;
            foreach (var type in typeToCheck.GetInterfaces())
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
                {
                    return type.GetGenericArguments().TryElementsOut(out param1, out param2);
                }
            }
            return false;
        }

        public static bool TryGetTypeParamsOfGenericInterface(this Type typeToCheck, Type genericInterfaceType, out Type param1, out Type param2, out Type param3)
        {
            param1 = null;
            param2 = null;
            param3 = null;
            foreach (var type in typeToCheck.GetInterfaces())
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
                {
                    return type.GetGenericArguments().TryElementsOut(out param1, out param2, out param3);
                }
            }
            return false;
        }

        public static bool IsOfGenericType(this Type typeToCheck, Type genericType)
        {
            return typeToCheck.IsOfGenericType(genericType, out Type _);
        }

        public static bool IsOfGenericType(this Type typeToCheck, Type genericType, out Type concreteGenericType)
        {
            if (genericType == null)
                throw new ArgumentNullException(nameof(genericType));

            if (!genericType.IsGenericTypeDefinition)
                throw new ArgumentException("The definition needs to be a GenericTypeDefinition", nameof(genericType));

            while (true)
            {
                concreteGenericType = null;

                if (typeToCheck == null || typeToCheck == typeof(object))
                    return false;

                if (typeToCheck == genericType)
                {
                    concreteGenericType = typeToCheck;
                    return true;
                }

                if ((typeToCheck.IsGenericType ? typeToCheck.GetGenericTypeDefinition() : typeToCheck) == genericType)
                {
                    concreteGenericType = typeToCheck;
                    return true;
                }

                if (genericType.IsInterface)
                {
                    foreach (var i in typeToCheck.GetInterfaces())
                    {
                        if (i.IsOfGenericType(genericType, out concreteGenericType))
                        {
                            return true;
                        }
                    }
                }

                typeToCheck = typeToCheck.BaseType;
            }
        }

        public static bool IsAssignableTo(this Type fromType, Type toType)
        {
            return toType.IsAssignableFrom(fromType);
        }

    }
}