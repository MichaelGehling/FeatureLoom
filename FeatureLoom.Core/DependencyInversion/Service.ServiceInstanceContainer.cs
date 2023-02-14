using FeatureLoom.Helpers;
using System;
using System.Threading;

namespace FeatureLoom.DependencyInversion
{
    public static partial class Service<T> where T:class
    {
        internal class ServiceInstanceContainer : IServiceInstanceContainer
        {
            T globalInstance;
            LazyValue<AsyncLocal<T>> localInstance;
            Func<T> createInstance;

            private ServiceInstanceContainer()
            {                
            }

            public static ServiceInstanceContainer Create(Func<T> createInstance)
            {
                var container = new ServiceInstanceContainer() { createInstance = createInstance, globalInstance = createInstance() };
                ServiceRegistry.RegisterService(container);
                return container;
            }

            public static ServiceInstanceContainer Create(Func<T> createInstance, T instance)
            {
                var container = new ServiceInstanceContainer() { createInstance = createInstance, globalInstance = instance };
                ServiceRegistry.RegisterService(container);
                return container;
            }

            public bool TryGetCreateServiceAction<T2>(out Func<T2> createServiceAction)
            {                
                createServiceAction = createInstance as Func<T2>;
                return createServiceAction != null;                
            }

            public bool UsesLocalInstance => localInstance.Exists;

            public T Instance
            {
                get => localInstance.Exists ? localInstance.Obj.Value ?? globalInstance : globalInstance;
                set
                {
                    if (localInstance.Exists) localInstance.Obj.Value = value;
                    else globalInstance = value;
                }
            }

            public void CreateLocalServiceInstance(T localServiceInstance)
            {

                localInstance.Obj.Value = localServiceInstance ?? createInstance();
            }

            public void CreateLocalServiceInstance()
            {
                localInstance.Obj.Value = createInstance();
            }

            public void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
            {
                if (localInstance.Exists && useLocalInstanceAsGlobal) globalInstance = localInstance.Obj.Value;
                localInstance.RemoveObj();
            }

            

            public Type ServiceType => typeof(T);
            object IServiceInstanceContainer.Instance => Instance;
        }
    }    
}
