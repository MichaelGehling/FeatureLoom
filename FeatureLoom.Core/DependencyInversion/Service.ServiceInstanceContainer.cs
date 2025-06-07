using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading;

namespace FeatureLoom.DependencyInversion
{
    public static partial class Service<T> where T : class
    {
        /// <summary>
        /// Container for managing a service instance of type <typeparamref name="T"/>.
        /// Supports both global (shared) and local (contextual, e.g. per-thread or per-async-context) instances.
        /// Handles instance creation, retrieval, and switching between global and local instances.
        /// </summary>
        internal class ServiceInstanceContainer : IServiceInstanceContainer
        {
            // Holds the global (shared) instance of the service.
            T globalInstance;

            // Holds the local (contextual) instance, e.g. per-thread or per-async-context.
            LazyValue<AsyncLocal<T>> localInstance;

            // The creator responsible for instantiating the service.
            IServiceInstanceCreator creator;

            // The name associated with this service instance (for named services).
            string serviceInstanceName;

            // Lock to ensure thread-safe creation of instances.
            MicroLock creationLock = new MicroLock();

            /// <summary>
            /// Initializes a new container with the given creator and service instance name.
            /// </summary>
            /// <param name="creator">The service instance creator.</param>
            /// <param name="serviceInstanceName">The name of the service instance.</param>
            public ServiceInstanceContainer(IServiceInstanceCreator creator, string serviceInstanceName)
            {
                this.creator = creator;
                this.serviceInstanceName = serviceInstanceName;
            }

            /// <summary>
            /// Initializes a new container by copying from another container, for a specific service instance name.
            /// Throws if the types are not compatible.
            /// </summary>
            /// <param name="container">The source container to copy from.</param>
            /// <param name="serviceInstanceName">The name of the service instance.</param>
            internal ServiceInstanceContainer(IServiceInstanceContainer container, string serviceInstanceName)
            {
                this.serviceInstanceName = serviceInstanceName;

                if (!typeof(T).IsAssignableFrom(container.ServiceType)) throw new Exception("Incompatible ServiceInstanceContainer used!");
                creator = container.ServiceInstanceCreator;
                globalInstance = container.GlobalInstance as T;
                if (container.UsesLocalInstance) CreateLocalServiceInstance(container.Instance as T);
            }

            /// <summary>
            /// Gets the name of this service instance (empty string for unnamed).
            /// </summary>
            public string ServiceInstanceName => serviceInstanceName;

            /// <summary>
            /// Gets the creator used to instantiate this service.
            /// </summary>
            public IServiceInstanceCreator ServiceInstanceCreator => creator;

            /// <summary>
            /// Gets whether this container is currently using a local (contextual) instance.
            /// </summary>
            public bool UsesLocalInstance => localInstance.Exists;

            /// <summary>
            /// Gets the global (shared) instance as an object.
            /// </summary>
            object IServiceInstanceContainer.GlobalInstance => globalInstance;

            /// <summary>
            /// Gets or sets the current service instance.
            /// If a local instance exists, it is used; otherwise, the global instance is used.
            /// Setting will update the local or global instance accordingly.
            /// </summary>
            public T Instance
            {
                get
                {
                    if (!localInstance.Exists)
                    {
                        if (globalInstance != null) return globalInstance;
                        using (creationLock.Lock())
                        {
                            if (globalInstance != null) return globalInstance;
                            globalInstance = creator.CreateServiceInstance<T>(serviceInstanceName);
                            return globalInstance;
                        }
                    }
                    else
                    {
                        T instance = localInstance.Obj.Value;
                        if (instance != null) return instance;
                        using (creationLock.Lock())
                        {
                            instance = localInstance.Obj.Value;
                            if (instance != null) return instance;

                            if (globalInstance != null) instance = globalInstance;
                            else instance = creator.CreateServiceInstance<T>(serviceInstanceName);
                            localInstance.Obj.Value = instance;
                            return instance;
                        }
                    }
                }
                set
                {
                    if (localInstance.Exists) localInstance.Obj.Value = value;
                    else globalInstance = value;
                }
            }

            /// <summary>
            /// Creates a new local (contextual) service instance, optionally using a provided instance.
            /// </summary>
            /// <param name="localServiceInstance">The instance to use, or null to create a new one.</param>
            public void CreateLocalServiceInstance(T localServiceInstance = null)
            {
                localInstance.Obj.Value = localServiceInstance ?? creator.CreateServiceInstance<T>();
            }

            /// <summary>
            /// Creates a new local (contextual) service instance using the creator.
            /// </summary>
            public void CreateLocalServiceInstance()
            {
                localInstance.Obj.Value = creator.CreateServiceInstance<T>();
            }

            /// <summary>
            /// Clears all local (contextual) service instances.
            /// Optionally sets the global instance to the last local instance.
            /// </summary>
            /// <param name="useLocalInstanceAsGlobal">If true, sets the global instance to the local instance before clearing.</param>
            public void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
            {
                if (localInstance.Exists && useLocalInstanceAsGlobal) globalInstance = localInstance.Obj.Value;
                localInstance.RemoveObj();
            }

            /// <summary>
            /// Gets the type of the service managed by this container.
            /// </summary>
            public Type ServiceType => typeof(T);

            /// <summary>
            /// Gets the type of the creator used for this service.
            /// </summary>
            public Type ServiceCreatorType => creator.ServiceType;

            /// <summary>
            /// Gets the current service instance as an object.
            /// </summary>
            object IServiceInstanceContainer.Instance => Instance;
        }
    }
}
