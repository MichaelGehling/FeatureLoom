using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using FeatureLoom.MetaDatas;
using FeatureLoom.Logging;

namespace FeatureLoom.DependencyInversion
{
    /// <summary>
    /// Provides a flexible, thread-safe service locator for type <typeparamref name="T"/>.
    /// Supports unnamed (default), named, and context-local service instances.
    /// Automatically creates instances if a parameterless constructor is available.
    /// Matches requested interfaces to concrete types, preferring already initialized instances.
    /// <para>
    /// <b>Disposal Notice:</b> If a service instance implements <see cref="IDisposable"/>, it will be disposed automatically
    /// when it is no longer referenced anywhere in the application (i.e., when it is garbage collected).
    /// This applies to both factory-created and user-supplied instances, including local/contextual instances.
    /// You do not need to manually dispose service instances managed by this class.
    /// </para>
    /// </summary>
    public static partial class Service<T> where T : class
    {
        // Holds the unnamed (default) service instance container.
        private static ServiceInstanceContainer unnamedInstanceContainer;

        // Holds named service instance containers, keyed by name.
        private static Dictionary<string, ServiceInstanceContainer> namedInstanceContainers = new Dictionary<string, ServiceInstanceContainer>();

        // Lock to ensure thread-safe modifications to service containers.
        private static MicroLock modifyLock = new MicroLock();

        // Indicates whether the service has been initialized.
        private static bool initialized = false;

        /// <summary>
        /// Initializes the service with a factory method for creating instances.
        /// Optionally resets all existing service instances.
        /// </summary>
        /// <param name="createServiceAction">Factory method to create service instances.</param>
        /// <param name="forceReset">If true, resets all existing instances before initializing.</param>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the created service instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced anywhere in the application.
        /// </remarks>
        public static void Init(Func<string, T> createServiceAction, bool forceReset = true)
        {
            if (forceReset) Reset();
            ServiceRegistry.RegisterCreator(new ServiceInstanceCreator(createServiceAction));
            initialized = true;            
        }

        /// <summary>
        /// Resets the service by unregistering and clearing all unnamed and named service instances.
        /// Thread-safe.
        /// </summary>
        /// <remarks>
        /// <b>Disposal Notice:</b> Service instances that implement <see cref="IDisposable"/> will be disposed automatically
        /// when they are no longer referenced.
        /// </remarks>
        public static void Reset()
        {
            using (modifyLock.Lock())
            {
                if (unnamedInstanceContainer != null)
                {                    
                    ServiceRegistry.UnregisterService(unnamedInstanceContainer);
                    unnamedInstanceContainer = null;
                }
                if (namedInstanceContainers.Count > 0)
                {                    
                    ServiceRegistry.UnregisterServices(namedInstanceContainers.Values);
                    namedInstanceContainers = new Dictionary<string, ServiceInstanceContainer>();
                }
            }
        }

        /// <summary>
        /// Gets whether the service has been initialized.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                if (!initialized) initialized = ServiceRegistry.TryGetServiceInstanceCreator<T>(out _);
                return initialized;
            }
        }

        /// <summary>
        /// Gets the unnamed (default) service instance.
        /// </summary>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static T Get()
        {
            if (unnamedInstanceContainer != null) return unnamedInstanceContainer.Instance;
            return HandleUninitializedGet("");
        }

        /// <summary>
        /// Sets the unnamed (default) service instance.
        /// </summary>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static void Set(T serviceInstance)
        {
            if (serviceInstance is IDisposable disposableService) disposableService.AttachDetructor(_ => disposableService.Dispose());
            if (unnamedInstanceContainer != null)
            {
                unnamedInstanceContainer.Instance = serviceInstance;
            }
            else
            {
                HandleUninitializedSet(serviceInstance, "");
            }
        }

        /// <summary>
        /// Gets a named service instance.
        /// </summary>
        /// <param name="serviceInstanceName">The name of the service instance.</param>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static T Get(string serviceInstanceName)
        {
            if (serviceInstanceName.EmptyOrNull()) return Get();

            if (namedInstanceContainers.TryGetValue(serviceInstanceName, out ServiceInstanceContainer instanceContainer)) return instanceContainer.Instance;
            return HandleUninitializedGet(serviceInstanceName);
        }

        /// <summary>
        /// Sets a named service instance.
        /// </summary>
        /// <param name="serviceInstanceName">The name of the service instance.</param>
        /// <param name="serviceInstance">The service instance to set.</param>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static void Set(string serviceInstanceName, T serviceInstance)
        {
            if (serviceInstance is IDisposable disposableService) disposableService.AttachDetructor(_ => disposableService.Dispose());

            if (serviceInstanceName.EmptyOrNull())
            {
                Set(serviceInstance);
                return;
            }

            if (namedInstanceContainers.TryGetValue(serviceInstanceName, out ServiceInstanceContainer instanceContainer))
            {                
                instanceContainer.Instance = serviceInstance;
            }
            else
            {                
                HandleUninitializedSet(serviceInstance, serviceInstanceName);
            }
        }

        /// <summary>
        /// Deletes the unnamed (default) service instance.
        /// </summary>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static void Delete()
        {
            if (unnamedInstanceContainer != null)
            {                
                unnamedInstanceContainer = null;
                ServiceRegistry.DeleteServiceInstanceContainer<T>("");
            }
        }

        /// <summary>
        /// Deletes a named service instance.
        /// </summary>
        /// <param name="serviceInstanceName">The name of the service instance to delete.</param>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static void Delete(string serviceInstanceName)
        {
            if (serviceInstanceName == null)
            {
                Delete();
                return;
            }

            if (namedInstanceContainers.Remove(serviceInstanceName))
            {                
                ServiceRegistry.DeleteServiceInstanceContainer<T>(serviceInstanceName);
            }
        }

        /// <summary>
        /// Gets or sets the unnamed (default) service instance.
        /// </summary>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static T Instance
        {
            get => Get();
            set => Set(value);
        }

        /// <summary>
        /// Creates a context-local unnamed service instance.
        /// </summary>
        /// <param name="localServiceInstance">Optional instance to use as the local instance.</param>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static void CreateLocalServiceInstance(T localServiceInstance = null) => CreateLocalServiceInstance("", localServiceInstance);

        /// <summary>
        /// Creates a context-local named service instance.
        /// </summary>
        /// <param name="serviceInstanceName">The name of the service instance.</param>
        /// <param name="localServiceInstance">Optional instance to use as the local instance.</param>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static void CreateLocalServiceInstance(string serviceInstanceName, T localServiceInstance = null)
        {
            if (localServiceInstance is IDisposable disposableService) disposableService.AttachDetructor(_ => disposableService.Dispose());

            if (serviceInstanceName == "")
            {
                if (unnamedInstanceContainer == null)
                {
                    if (!ServiceRegistry.TryGetServiceInstanceContainer(serviceInstanceName, out ServiceInstanceContainer newContainer))
                        throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
                    unnamedInstanceContainer = newContainer;
                }                
                unnamedInstanceContainer.CreateLocalServiceInstance(localServiceInstance);
            }
            else
            {
                if (!namedInstanceContainers.TryGetValue(serviceInstanceName, out ServiceInstanceContainer instanceContainer))
                {
                    using (modifyLock.Lock())
                    {
                        if (!ServiceRegistry.TryGetServiceInstanceContainer<T>(serviceInstanceName, out instanceContainer))
                            throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
                        namedInstanceContainers.Add(serviceInstanceName, instanceContainer);
                    }
                }                
                instanceContainer.CreateLocalServiceInstance(localServiceInstance);
            }
        }

        /// <summary>
        /// Clears all context-local service instances, optionally using the local instance as the new global instance.
        /// </summary>
        /// <param name="useLocalInstanceAsGlobal">If true, replaces the global instance with the local instance.</param>
        /// <remarks>
        /// <b>Disposal Notice:</b> If the instance implements <see cref="IDisposable"/>, it will be disposed automatically
        /// when it is no longer referenced.
        /// </remarks>
        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            if (unnamedInstanceContainer != null) unnamedInstanceContainer.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
            var namedInstanceContainersCopy = namedInstanceContainers;
            foreach (var container in namedInstanceContainersCopy.Values) container.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
        }

        // Internal helper: Handles setting an instance when not yet initialized.
        private static void HandleUninitializedSet(T value, string serviceInstanceName)
        {
            if (value == null) return;
            if (!ServiceRegistry.TryGetServiceInstanceCreatorFromType(value.GetType(), out IServiceInstanceCreator creator))
                throw new Exception($"Cannot create Service from instance type {value.GetType()}. Please initialize it first.");
            ServiceInstanceContainer newContainer = new ServiceInstanceContainer(creator, serviceInstanceName);
            ServiceRegistry.RegisterService(newContainer);
            newContainer.Instance = value;

            if (serviceInstanceName == "")
            {
                unnamedInstanceContainer = newContainer;
            }
            else using (modifyLock.Lock())
            {
                var namedInstanceContainersCopy = namedInstanceContainers;
                Dictionary<string, ServiceInstanceContainer> newNamedInstanceContainers = new Dictionary<string, ServiceInstanceContainer>(namedInstanceContainersCopy);
                newNamedInstanceContainers.Add(serviceInstanceName, newContainer);
                namedInstanceContainers = newNamedInstanceContainers;
            }
        }

        // Internal helper: Handles getting an instance when not yet initialized.
        private static T HandleUninitializedGet(string serviceInstanceName)
        {
            if (!ServiceRegistry.TryGetServiceInstanceContainer<T>(serviceInstanceName, out ServiceInstanceContainer newContainer))
                throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
            if (serviceInstanceName == "")
            {
                unnamedInstanceContainer = newContainer;
            }
            else using (modifyLock.Lock())
            {
                var namedInstanceContainersCopy = namedInstanceContainers;
                Dictionary<string, ServiceInstanceContainer> newNamedInstanceContainers = new Dictionary<string, ServiceInstanceContainer>(namedInstanceContainersCopy);
                newNamedInstanceContainers.Add(serviceInstanceName, newContainer);
                namedInstanceContainers = newNamedInstanceContainers;
            }
            return newContainer.Instance;
        }
    }
}
