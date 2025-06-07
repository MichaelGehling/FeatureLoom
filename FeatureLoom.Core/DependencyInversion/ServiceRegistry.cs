using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.DependencyInversion
{
    /// <summary>
    /// Central static registry for managing service instance containers and their creators.
    /// Supports named and unnamed services, interface-to-concrete resolution, and context-local instances.
    /// Ensures thread safety for all operations.
    /// </summary>
    public static class ServiceRegistry
    {
        // Stores all registered service instance containers, keyed by type and name.
        static Dictionary<TypeAndName, IServiceInstanceContainer> services = new Dictionary<TypeAndName, IServiceInstanceContainer>();

        // Stores all registered service instance creators, keyed by service type.
        static Dictionary<Type, IServiceInstanceCreator> creators = new Dictionary<Type, IServiceInstanceCreator>();

        // Lock to ensure thread-safe access to the registry.
        static MicroLock registryLock = new MicroLock();

        // Indicates if local (contextual) instances are active for all services.
        static bool localInstancesForAllServicesActive = false;

        /// <summary>
        /// Gets or sets whether the registry is allowed to search all loaded assemblies for a suitable implementation
        /// when resolving a service type (e.g., for interfaces or abstract classes).
        /// </summary>
        public static bool AllowToSearchAssembly { get; set; } = true;

        /// <summary>
        /// Gets whether local (contextual) instances are active for all services.
        /// </summary>
        public static bool LocalInstancesForAllServicesActive => localInstancesForAllServicesActive;

        /// <summary>
        /// Registers a service instance container in the registry.
        /// If local instances are globally active, ensures the container uses a local instance.
        /// </summary>
        /// <param name="service">The service instance container to register.</param>
        internal static void RegisterService(IServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                if (localInstancesForAllServicesActive && !service.UsesLocalInstance) service.CreateLocalServiceInstance();
                services[service.GetTypeAndName()] = service;
            }
        }

        /// <summary>
        /// Unregisters a service instance container from the registry.
        /// </summary>
        /// <param name="serviceToRemove">The service instance container to remove.</param>
        internal static void UnregisterService(IServiceInstanceContainer serviceToRemove)
        {
            using (registryLock.Lock())
            {
                services.Remove(serviceToRemove.GetTypeAndName());
            }
        }

        /// <summary>
        /// Unregisters multiple service instance containers from the registry.
        /// </summary>
        /// <param name="servicesToRemove">The service instance containers to remove.</param>
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

        /// <summary>
        /// Returns all currently registered service instance containers.
        /// </summary>
        public static IServiceInstanceContainer[] GetAllRegisteredServices()
        {
            using (registryLock.Lock())
            {
                return services.Values.ToArray();
            }
        }

        /// <summary>
        /// Registers a service instance creator for a specific type.
        /// </summary>
        /// <param name="creator">The service instance creator to register.</param>
        internal static void RegisterCreator(IServiceInstanceCreator creator)
        {
            using (registryLock.Lock())
            {
                creators[creator.ServiceType] = creator;
            }
        }

        /// <summary>
        /// Unregisters a service instance creator for a specific type.
        /// </summary>
        /// <param name="creator">The service instance creator to unregister.</param>
        internal static void UnregisterCreator(IServiceInstanceCreator creator)
        {
            using (registryLock.Lock())
            {
                creators.Remove(creator.ServiceType);
            }
        }

        /// <summary>
        /// Activates local (contextual) instances for all registered services.
        /// </summary>
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

        /// <summary>
        /// Clears all local (contextual) service instances for all services.
        /// Optionally replaces the global instance with the local instance.
        /// </summary>
        /// <param name="useLocalInstanceAsGlobal">If true, uses the local instance as the new global instance.</param>
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

        /// <summary>
        /// Tries to get a service instance creator for a given type using reflection.
        /// </summary>
        /// <param name="type">The service type.</param>
        /// <param name="creator">The found creator, or null if not found.</param>
        /// <returns>True if a creator was found; otherwise, false.</returns>
        internal static bool TryGetServiceInstanceCreatorFromType(Type type, out IServiceInstanceCreator creator)
        {
            MethodInfo method = typeof(ServiceRegistry).GetMethod(
                nameof(ServiceRegistry.GetServiceInstanceCreator),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            );
            method = method.MakeGenericMethod(type);
            creator = (IServiceInstanceCreator)method.Invoke(null, Array.Empty<object>());
            return creator != null;
        }

        /// <summary>
        /// Gets a service instance creator for a generic type parameter.
        /// </summary>
        private static IServiceInstanceCreator GetServiceInstanceCreator<T>() where T : class
        {
            TryGetServiceInstanceCreator<T>(out var creator);
            return creator;
        }

        /// <summary>
        /// Tries to get a service instance creator for a generic type parameter.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <param name="creator">The found creator, or null if not found.</param>
        /// <returns>True if a creator was found; otherwise, false.</returns>
        internal static bool TryGetServiceInstanceCreator<T>(out IServiceInstanceCreator creator) where T : class
        {
            using (registryLock.Lock())
            {
                return TryGetServiceInstanceCreatorUnsafe<T>(out creator);
            }
        }

        /// <summary>
        /// Tries to get a service instance creator for a generic type parameter without locking.
        /// Searches for compatible creators and falls back to assembly scanning if enabled.
        /// </summary>
        private static bool TryGetServiceInstanceCreatorUnsafe<T>(out IServiceInstanceCreator creator) where T : class
        {
            var type = typeof(T);

            if (creators.TryGetValue(type, out creator)) return true;

            // Try to find a compatible creator for an assignable type (e.g., interface to concrete).
            foreach (var otherCreator in creators.Values)
            {
                if (type.IsAssignableFrom(otherCreator.ServiceType))
                {
                    creator = otherCreator;
                    creators[type] = creator;
                    return true;
                }
            }

            // Try to find a default constructor, or scan assemblies if allowed.
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null && AllowToSearchAssembly)
            {
                // NOTE: May be non-deterministic if multiple implementations exist.
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

        /// <summary>
        /// Tries to get a registered service instance container for a given type and name.
        /// If not found, attempts to create one using a compatible creator.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <param name="serviceInstanceName">The name of the service instance.</param>
        /// <param name="instanceContainer">The found or created instance container.</param>
        /// <returns>True if a container was found or created; otherwise, false.</returns>
        internal static bool TryGetServiceInstanceContainer<T>(string serviceInstanceName, out Service<T>.ServiceInstanceContainer instanceContainer) where T : class
        {
            instanceContainer = null;
            var typeAndName = new TypeAndName(typeof(T), serviceInstanceName);

            using (registryLock.Lock())
            {
                // Try to find an exact match.
                if (services.TryGetValue(typeAndName, out IServiceInstanceContainer container) && container is Service<T>.ServiceInstanceContainer typedContainer)
                {
                    instanceContainer = typedContainer;
                    return true;
                }

                // Try to find a compatible service (e.g., interface to concrete).
                foreach (var otherService in services.Values)
                {
                    if (typeAndName.type.IsAssignableFrom(otherService.ServiceType) &&
                        otherService.ServiceInstanceName == serviceInstanceName)
                    {
                        instanceContainer = new Service<T>.ServiceInstanceContainer(otherService, serviceInstanceName);
                        services[typeAndName] = instanceContainer;
                        return true;
                    }
                }

                // Try to create a new container using a creator.
                if (!TryGetServiceInstanceCreatorUnsafe<T>(out IServiceInstanceCreator creator)) return false;
                instanceContainer = new Service<T>.ServiceInstanceContainer(creator, serviceInstanceName);
                services[typeAndName] = instanceContainer;
                return true;
            }
        }

        /// <summary>
        /// Deletes a service instance container for a given type and name.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <param name="serviceInstanceName">The name of the service instance to delete.</param>
        internal static void DeleteServiceInstanceContainer<T>(string serviceInstanceName) where T : class
        {
            var typeAndName = new TypeAndName(typeof(T), serviceInstanceName);
            using (registryLock.Lock())
            {
                services.Remove(typeAndName);
            }
        }

        /// <summary>
        /// Helper extension to get the composite key for a service instance container.
        /// </summary>
        private static TypeAndName GetTypeAndName(this IServiceInstanceContainer service) => new TypeAndName(service.ServiceType, service.ServiceInstanceName);

        /// <summary>
        /// Composite key for identifying service instance containers by type and name.
        /// </summary>
        private readonly struct TypeAndName : IEquatable<TypeAndName>
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