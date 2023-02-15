using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.DependencyInversion
{
    public static class ServiceRegistry
    {
        static Dictionary<Type, IServiceInstanceContainer> services = new Dictionary<Type, IServiceInstanceContainer>();
        static Dictionary<Type, IServiceInstanceCreator> creators = new Dictionary<Type, IServiceInstanceCreator>();
        static MicroLock registryLock = new MicroLock();
        static bool localInstancesForAllServicesActive = false;

        public static bool LocalInstancesForAllServicesActive => localInstancesForAllServicesActive;

        internal static void RegisterService(IServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                services[service.ServiceType] = service;
            }
        }

        internal static void UnregisterService(IServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                services.Remove(service.ServiceType);
            }
        }

        public static IServiceInstanceContainer[] GetAllRegisteredServices()
        {
            using(registryLock.Lock())
            {
                return services.Values.ToArray();
            }
        }

        internal static void RegisterCreator(IServiceInstanceCreator creator)
        {
            using (registryLock.Lock())
            {
                creators[creator.ServiceType] = creator;
            }
        }

        internal static void UnregisterCreator(IServiceInstanceCreator creator)
        {
            using (registryLock.Lock())
            {
                creators.Remove(creator.ServiceType);
            }
        }

        public static void CreateLocalInstancesForAllServices()
        {
            using (registryLock.Lock())
            {
                localInstancesForAllServicesActive = true;
                foreach (var service in services.Values)
                {
                    service.CreateLocalServiceInstance();
                }
            }
        }

        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            using (registryLock.Lock())
            {
                localInstancesForAllServicesActive = false;
                foreach (var service in services.Values)
                {
                    service.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
                }
            }
        }

        internal static bool TryGetServiceInstanceCreatorFromType(Type type, out IServiceInstanceCreator creator)
        {            
            MethodInfo method = typeof(ServiceRegistry).GetMethod("GetServiceInstanceCreator");
            method = method.MakeGenericMethod(type);
            creator = (IServiceInstanceCreator) method.Invoke(null, Array.Empty<object>());
            return creator != null;
        }

        private static IServiceInstanceCreator GetServiceInstanceCreator<T>() where T : class
        {
            TryGetServiceInstanceCreator<T>(out var creator);
            return creator;
        }

        internal static bool TryGetServiceInstanceCreator<T>(out IServiceInstanceCreator creator) where T : class
        {
            using(registryLock.Lock())
            {
                return TryGetServiceInstanceCreatorUnsafe<T>(out creator);
            }
        }

        private static bool TryGetServiceInstanceCreatorUnsafe<T>(out IServiceInstanceCreator creator) where T : class
        {
            var type = typeof(T);

            if (creators.TryGetValue(type, out creator)) return true;

            foreach (var c in creators.Values)
            {
                if (type.IsAssignableFrom(c.ServiceType))
                {
                    creator = c;
                    creators[type] = creator;
                    return true;
                }
            }

            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                // Searching all assemblies for a Type that fits as a fallback.
                // NOTE: Be aware that it is not guaranteed which implementation will be used for an interface type, if multiple classes implement it.
                //       This might also fail with a trimmed binary where unused code is stripped at build time to improve the size of the binary.
                //       In such cases the wanted service type must be initialized explicitly before the interface type is requested.
                var alternativeType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes())
                    .FirstOrDefault(p => type.IsAssignableFrom(p) && p.GetConstructor(Type.EmptyTypes) != null);
                constructor = alternativeType?.GetConstructor(Type.EmptyTypes);
            }

            if (constructor != null)
            {
                creator = new Service<T>.ServiceInstanceCreator(_ => (T)constructor.Invoke(Array.Empty<object>()));
                creators[type] = creator;
                return true;
            }
            creator = null;
            return false;
        }

        internal static bool TryGetServiceInstanceContainer<T>(out Service<T>.ServiceInstanceContainer instanceContainer) where T : class
        {
            instanceContainer = null;

            using (registryLock.Lock())
            {
                var type = typeof(T);

                if (services.TryGetValue(type, out IServiceInstanceContainer container) && container is Service<T>.ServiceInstanceContainer typedContainer)
                {
                    instanceContainer = typedContainer;
                    return true;
                }

                foreach(var c in services.Values)
                {
                    if (type.IsAssignableFrom(c.ServiceType))
                    {
                        instanceContainer = new Service<T>.ServiceInstanceContainer(c);
                        services[type] = instanceContainer;
                        return true;
                    }
                }
                
                if (!TryGetServiceInstanceCreatorUnsafe<T>(out IServiceInstanceCreator creator)) return false;
                instanceContainer = new Service<T>.ServiceInstanceContainer(creator);
                services[type] = instanceContainer;
                return true;
            }
        }

    }
}