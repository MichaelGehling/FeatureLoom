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
        static Dictionary<TypeAndName, IPreparedServiceInstanceContainer> services = new Dictionary<TypeAndName, IPreparedServiceInstanceContainer>();

        // Stores all registered service instance creators, keyed by service type.
        static Dictionary<Type, IServiceInstanceCreator> creators = new Dictionary<Type, IServiceInstanceCreator>();

        // Lock to ensure thread-safe access to the registry.
        static MicroLock registryLock = new MicroLock();

        // New containers need local storage even if registered outside an already existing scope.
        static bool localScopeStorageEnabled = false;
        static readonly AsyncLocal<LocalServiceScope> currentLocalScope = new AsyncLocal<LocalServiceScope>();
        static readonly List<WeakReference<LocalServiceScope>> localScopes = new List<WeakReference<LocalServiceScope>>();

        internal static LocalServiceScope CurrentLocalScope
        {
            get
            {
                var scope = currentLocalScope.Value;
                return scope != null && scope.IsActive ? scope : null;
            }
        }

        /// <summary>
        /// Gets or sets whether the registry is allowed to search all loaded assemblies for a suitable implementation
        /// when resolving a service type (e.g., for interfaces or abstract classes).
        /// </summary>
        public static bool AllowToSearchAssembly { get; set; } = true;

        /// <summary>
        /// Gets whether the current execution context has an active local-service scope.
        /// </summary>
        public static bool LocalInstancesForAllServicesActive => CurrentLocalScope != null;

        /// <summary>
        /// Registers a service instance container in the registry.
        /// If local instances are globally active, ensures the container uses a local instance.
        /// </summary>
        /// <param name="service">The service instance container to register.</param>
        internal static void RegisterService(IPreparedServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                if (localScopeStorageEnabled) service.EnableLocalServiceInstances();
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
                return services.Values.ToArray<IServiceInstanceContainer>();
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
        /// Creates a new local-service scope shared by the current execution context and its descendants.
        /// Services first registered or resolved later also use this scope. Separate activations have independent instances.
        /// Prepares all current-context slots before constructing services, so dependencies use the new local instances.
        /// Concurrent clearing discards pending local instances; construction already in progress may still complete.
        /// </summary>
        public static void CreateLocalInstancesForAllServices()
        {
            Action[] initialize;
            using (registryLock.Lock())
            {
                var scope = new LocalServiceScope();
                currentLocalScope.Value = scope;
                localScopeStorageEnabled = true;
                localScopes.RemoveAll(reference => !reference.TryGetTarget(out var existing) || !existing.IsActive);
                localScopes.Add(new WeakReference<LocalServiceScope>(scope));
                initialize = new Action[services.Count];
                int index = 0;
                foreach (var service in services.Values)
                {
                    initialize[index++] = service.PrepareLocalServiceInstance();
                }
            }

            // Factories can resolve or register services, and must never run under the registry lock.
            foreach (var initializeService in initialize) initializeService();
        }

        /// <summary>
        /// Clears the current local-service scope, including its descendants, without clearing independent scopes.
        /// Subsequent accesses fall back to global instances unless an explicit context-local override applies.
        /// </summary>
        /// <param name="useLocalInstanceAsGlobal">If true, promotes completed instances from the caller's scope to global instances.</param>
        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            ClearAllLocalServiceInstances(useLocalInstanceAsGlobal, false);
        }

        /// <summary>
        /// Clears the current shared local-service scope, or explicitly clears all contexts.
        /// In-progress factories may finish, but cannot restore cleared scope entries.
        /// </summary>
        /// <param name="useLocalInstanceAsGlobal">If true, promotes only the caller's completed local instances to globals.</param>
        /// <param name="allContexts">If true, clears all scopes and context-local overrides; otherwise clears only the caller's scope and overrides.</param>
        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal, bool allContexts = false)
        {
            using (registryLock.Lock())
            {
                var currentScope = CurrentLocalScope;
                foreach (var service in services.Values)
                {
                    service.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal, allContexts);
                }
                if (allContexts)
                {
                    foreach (var reference in localScopes)
                    {
                        if (reference.TryGetTarget(out var scope)) scope.Clear();
                    }
                    localScopes.Clear();
                    localScopeStorageEnabled = false;
                }
                else currentScope?.Clear();
                currentLocalScope.Value = null;
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
                if (services.TryGetValue(typeAndName, out var container) && container is Service<T>.ServiceInstanceContainer typedContainer)
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
                        if (localScopeStorageEnabled) instanceContainer.EnableLocalServiceInstances();
                        services[typeAndName] = instanceContainer;
                        return true;
                    }
                }

                // Try to create a new container using a creator.
                if (!TryGetServiceInstanceCreatorUnsafe<T>(out IServiceInstanceCreator creator)) return false;
                instanceContainer = new Service<T>.ServiceInstanceContainer(creator, serviceInstanceName);
                if (localScopeStorageEnabled) instanceContainer.EnableLocalServiceInstances();
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