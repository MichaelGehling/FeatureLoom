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
            IPreparedServiceInstanceContainer sourceContainer;

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
            internal ServiceInstanceContainer(IPreparedServiceInstanceContainer container, string serviceInstanceName)
            {
                this.serviceInstanceName = serviceInstanceName;

                if (!typeof(T).IsAssignableFrom(container.ServiceType)) throw new Exception("Incompatible ServiceInstanceContainer used!");
                creator = container.ServiceInstanceCreator;
                sourceContainer = container;
                globalInstance = container.GlobalInstance as T;
                // The source's Instance getter may run a factory. Defer it until after registry publication.
                if (container.UsesLocalInstance)
                {
                    var scope = ServiceRegistry.CurrentLocalScope;
                    var localInstances = localInstance.Obj;
                    var local = new LocalInstance(CreateInstance, scope: scope);
                    if (scope != null) scope.Set(localInstances, local);
                    else localInstances.Value = local;
                }
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
            public bool UsesLocalInstance
            {
                get
                {
                    var localInstances = localInstance.ObjIfExists;
                    if (localInstances == null) return false;
                    var scope = ServiceRegistry.CurrentLocalScope;
                    var local = GetExistingLocalInstance(localInstances, scope);
                    return local != LocalInstance.Cleared && (local != null || scope != null);
                }
            }

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
                    if (localInstances != null)
                    {
                        var scope = ServiceRegistry.CurrentLocalScope;
                        var local = GetExistingLocalInstance(localInstances, scope);
                        if (local == null && scope != null)
                        {
                            // Publish the holder in the shared scope before invoking its factory.
                            local = scope.GetOrAdd(localInstances, new LocalInstance(CreateInstance, scope: scope));
                        }
                        if (local != null && local != LocalInstance.Cleared) return local.Instance;
                    }
                    if (globalInstance != null) return globalInstance;
                    return GetGlobalInstance();
                }
                set
                {
                    var localInstances = localInstance.ObjIfExists;
                    if (localInstances != null)
                    {
                        var scope = ServiceRegistry.CurrentLocalScope;
                        var localOverride = localInstances.Value;
                        if (localOverride != null && ReferenceEquals(localOverride.Scope, scope))
                        {
                            localInstances.Value = value == null ? null : new LocalInstance(CreateInstance, value, scope);
                            return;
                        }
                        if (scope != null)
                        {
                            scope.Set(localInstances, new LocalInstance(CreateInstance, value, scope));
                            return;
                        }
                    }
                    globalInstance = value;
                }
            }

            object IPreparedServiceInstanceContainer.GetGlobalInstance() => GetGlobalInstance();

            private T GetGlobalInstance()
            {
                if (globalInstance != null) return globalInstance;
                using (creationLock.Lock())
                {
                    if (globalInstance != null) return globalInstance;
                    // An alias falling back to global must not capture its source's active local instance.
                    globalInstance = sourceContainer != null
                        ? (T)sourceContainer.GetGlobalInstance()
                        : creator.CreateServiceInstance<T>(serviceInstanceName);
                    return globalInstance;
                }
            }

            private T CreateInstance() => sourceContainer != null
                ? (T)sourceContainer.Instance
                : creator.CreateServiceInstance<T>(serviceInstanceName);

            private LocalInstance GetExistingLocalInstance(AsyncLocal<LocalInstance> localInstances, LocalServiceScope scope)
            {
                var localOverride = localInstances.Value;
                if (localOverride != null && ReferenceEquals(localOverride.Scope, scope)) return localOverride;
                return scope != null && scope.TryGet<LocalInstance>(localInstances, out var local) ? local : null;
            }

            /// <summary>
            /// Creates a new local (contextual) service instance, optionally using a provided instance.
            /// </summary>
            /// <param name="localServiceInstance">The instance to use, or null to create a new one.</param>
            public void CreateLocalServiceInstance(T localServiceInstance = null)
            {
                var local = new LocalInstance(() => creator.CreateServiceInstance<T>(serviceInstanceName), localServiceInstance,
                    ServiceRegistry.CurrentLocalScope);
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
                var scope = ServiceRegistry.CurrentLocalScope;
                var localInstances = localInstance.Obj;
                var local = new LocalInstance(CreateInstance, scope: scope);
                scope.Set(localInstances, local);
                return () =>
                {
                    // Clearing closes the scope or detaches storage; explicit overrides can replace this slot.
                    if (scope.IsActive && ReferenceEquals(ServiceRegistry.CurrentLocalScope, scope) &&
                        ReferenceEquals(localInstance.ObjIfExists, localInstances) &&
                        ReferenceEquals(GetExistingLocalInstance(localInstances, scope), local))
                        _ = local.Instance;
                };
            }

            /// <summary>
            /// Clears this service in the current scope and removes the current context's explicit override.
            /// Optionally sets the global instance to the current context's completed local instance.
            /// Pending or failed initialization does not replace the global instance.
            /// </summary>
            /// <param name="useLocalInstanceAsGlobal">If true, sets the global instance to the local instance before clearing.</param>
            public void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
            {
                ClearAllLocalServiceInstances(useLocalInstanceAsGlobal, false);
            }

            public void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal, bool allContexts)
            {
                var scope = ServiceRegistry.CurrentLocalScope;
                var localInstances = allContexts ? localInstance.ExchangeObj(null) : localInstance.ObjIfExists;
                if (localInstances == null) return;
                var completedInstance = GetExistingLocalInstance(localInstances, scope)?.ExistingInstance;
                if (useLocalInstanceAsGlobal && completedInstance != null) globalInstance = completedInstance;
                localInstances.Value = null;
                if (!allContexts && scope != null) scope.Set(localInstances, LocalInstance.Cleared);
            }

            private sealed class LocalInstance
            {
                public static readonly LocalInstance Cleared = new LocalInstance(null);
                private readonly Func<T> create;
                private readonly object sync = new object();
                private T instance;
                private bool creating;

                public LocalServiceScope Scope { get; }

                public LocalInstance(Func<T> create, T instance = null, LocalServiceScope scope = null)
                {
                    this.create = create;
                    this.instance = instance;
                    Scope = scope;
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
