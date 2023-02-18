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
        static Dictionary<TypeAndName, IServiceInstanceContainer> services = new Dictionary<TypeAndName, IServiceInstanceContainer>();
        static Dictionary<Type, IServiceInstanceCreator> creators = new Dictionary<Type, IServiceInstanceCreator>();
        static MicroLock registryLock = new MicroLock();
        static bool localInstancesForAllServicesActive = false;
        static public bool AllowToSearchAssembly { get; set; } = true;

        public static bool LocalInstancesForAllServicesActive => localInstancesForAllServicesActive;

        internal static void RegisterService(IServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                if (localInstancesForAllServicesActive && !service.UsesLocalInstance) service.CreateLocalServiceInstance();
                services[service.GetTypeAndName()] = service;
            }
        }

        internal static void UnregisterService(IServiceInstanceContainer serviceToRemove)
        {
            using (registryLock.Lock())
            {
                services.Remove(serviceToRemove.GetTypeAndName());
            }
        }

        internal static void UnregisterServices(IEnumerable<IServiceInstanceContainer> servicesToRemove)
        {
            using (registryLock.Lock())
            {
                foreach (var serviceToRemove in servicesToRemove)
                {
                    services.Remove(serviceToRemove.GetTypeAndName());
                }
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

            foreach (var otherCreator in creators.Values)
            {
                if (type.IsAssignableFrom(otherCreator.ServiceType))
                {
                    creator = otherCreator;
                    creators[type] = creator;
                    return true;
                }
            }

            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null && AllowToSearchAssembly)
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

        internal static bool TryGetServiceInstanceContainer<T>(string serviceInstanceName, out Service<T>.ServiceInstanceContainer instanceContainer) where T : class
        {
            instanceContainer = null;
            var typeAndName = new TypeAndName(typeof(T), serviceInstanceName);

            using (registryLock.Lock())
            {                
                if (services.TryGetValue(typeAndName, out IServiceInstanceContainer container) && container is Service<T>.ServiceInstanceContainer typedContainer)
                {
                    instanceContainer = typedContainer;
                    return true;
                }

                foreach(var otherService in services.Values)
                {
                    if (typeAndName.type.IsAssignableFrom(otherService.ServiceType) && 
                        otherService.ServiceInstanceName == serviceInstanceName)
                    {
                        instanceContainer = new Service<T>.ServiceInstanceContainer(otherService, serviceInstanceName);
                        services[typeAndName] = instanceContainer;
                        return true;
                    }
                }
                
                if (!TryGetServiceInstanceCreatorUnsafe<T>(out IServiceInstanceCreator creator)) return false;
                instanceContainer = new Service<T>.ServiceInstanceContainer(creator, serviceInstanceName);
                services[typeAndName] = instanceContainer;
                return true;
            }
        }

        static TypeAndName GetTypeAndName(this IServiceInstanceContainer service) => new TypeAndName(service.ServiceType, service.ServiceInstanceName);

        readonly struct TypeAndName : IEquatable<TypeAndName>
        {
            public readonly Type type;
            public readonly string name;

            public TypeAndName(Type type, string name)
            {
                this.type = type;
                this.name = name;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is TypeAndName other)) return false;
                return Equals(other);
            }

            public bool Equals(TypeAndName other)
            {
                if (type != other.type) return false;
                if (name != other.name) return false;
                return true;
            }

            public override int GetHashCode()
            {
                return type.GetHashCode() ^ name.GetHashCode();
            }

            public override string ToString()
            {
                return $"{type.ToString()}:{name}";
            }
        }
    }
}