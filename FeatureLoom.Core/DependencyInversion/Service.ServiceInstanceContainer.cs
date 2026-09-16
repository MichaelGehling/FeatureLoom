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
        internal class ServiceInstanceContainer : IPreparedServiceInstanceContainer
        {
            // Holds the global (shared) instance of the service.
            T globalInstance;

            // Holds the local (contextual) instance, e.g. per-thread or per-async-context.
            LazyValue<AsyncLocal<LocalInstance>> localInstance;

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
                // The source's Instance getter may run a factory. Defer it until after registry publication.
                if (container.UsesLocalInstance) localInstance.Obj.Value = new LocalInstance(() => container.Instance as T);
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
                    var localInstances = localInstance.ObjIfExists;
                    if (localInstances == null)
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
                        var local = localInstances.Value;
                        if (local == null)
                        {
                            // Manual local overrides retain global fallback in other contexts; global local-mode does not.
                            var fallback = ServiceRegistry.LocalInstancesForAllServicesActive ? null : globalInstance;
                            local = new LocalInstance(() => creator.CreateServiceInstance<T>(serviceInstanceName), fallback);
                            localInstances.Value = local;
                        }
                        return local.Instance;
                    }
                }
                set
                {
                    var localInstances = localInstance.ObjIfExists;
                    if (localInstances != null) localInstances.Value = value == null ? null : new LocalInstance(() => creator.CreateServiceInstance<T>(serviceInstanceName), value);
                    else globalInstance = value;
                }
            }

            /// <summary>
            /// Creates a new local (contextual) service instance, optionally using a provided instance.
            /// </summary>
            /// <param name="localServiceInstance">The instance to use, or null to create a new one.</param>
            public void CreateLocalServiceInstance(T localServiceInstance = null)
            {
                var local = new LocalInstance(() => creator.CreateServiceInstance<T>(serviceInstanceName), localServiceInstance);
                localInstance.Obj.Value = local;
                _ = local.Instance;
            }

            /// <summary>
            /// Creates a new local (contextual) service instance using the creator.
            /// </summary>
            public void CreateLocalServiceInstance()
            {
                CreateLocalServiceInstance(null);
            }

            public void EnableLocalServiceInstances()
            {
                _ = localInstance.Obj;
            }

            public Action PrepareLocalServiceInstance()
            {
                var localInstances = localInstance.Obj;
                var local = new LocalInstance(() => creator.CreateServiceInstance<T>(serviceInstanceName));
                localInstances.Value = local;
                return () =>
                {
                    // Clearing detaches the holder; another activation in this context replaces the slot.
                    if (ReferenceEquals(localInstance.ObjIfExists, localInstances) && ReferenceEquals(localInstances.Value, local))
                        _ = local.Instance;
                };
            }

            /// <summary>
            /// Clears all local (contextual) service instances.
            /// Optionally sets the global instance to the current context's completed local instance.
            /// Pending or failed initialization does not replace the global instance.
            /// </summary>
            /// <param name="useLocalInstanceAsGlobal">If true, sets the global instance to the local instance before clearing.</param>
            public void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
            {
                var localInstances = localInstance.ExchangeObj(null);
                var completedInstance = localInstances?.Value?.ExistingInstance;
                if (useLocalInstanceAsGlobal && completedInstance != null) globalInstance = completedInstance;
            }

            private sealed class LocalInstance
            {
                private readonly Func<T> create;
                private readonly object sync = new object();
                private T instance;
                private bool creating;

                public LocalInstance(Func<T> create, T instance = null)
                {
                    this.create = create;
                    this.instance = instance;
                }

                public T ExistingInstance => Volatile.Read(ref instance);

                public T Instance
                {
                    get
                    {
                        var value = ExistingInstance;
                        if (value != null) return value;
                        lock (sync)
                        {
                            if (instance != null) return instance;
                            if (creating) throw new InvalidOperationException($"Circular local service initialization for {typeof(T)}.");
                            creating = true;
                            try
                            {
                                value = create();
                                Volatile.Write(ref instance, value);
                                return value;
                            }
                            finally
                            {
                                // Failed factories can be retried without falling back to an old global instance.
                                creating = false;
                            }
                        }
                    }
                }
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
