using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FeatureLoom.Services
{
    public static class Service<T> where T:class
    {
        private static ServiceInstanceContainer instanceContainer;

        public static void Init(Func<T> createServiceAction)
        {
            if (instanceContainer == null) Interlocked.CompareExchange(ref instanceContainer, ServiceInstanceContainer.Create(createServiceAction), null);
        }

        public static void Init<DFLT>() where DFLT : T, new()
        {
            if (instanceContainer == null) Interlocked.CompareExchange(ref instanceContainer, ServiceInstanceContainer.Create<DFLT>(), null);
        }

        public static T GetInstance<DFLT>() where DFLT : T, new()
        {
            if (instanceContainer == null) Interlocked.CompareExchange(ref instanceContainer, ServiceInstanceContainer.Create<DFLT>(), null);
            return instanceContainer.Instance;
        }

        public static void SetInstance<DFLT>(DFLT serviceInstance) where DFLT : T, new()
        {            
            if (instanceContainer == null) Interlocked.CompareExchange(ref instanceContainer, ServiceInstanceContainer.Create<DFLT>(serviceInstance), null);
            instanceContainer.Instance = serviceInstance;
        }

        public static void CreateLocalServiceInstance<DFLT>(DFLT localServiceInstance) where DFLT : T, new()
        {
            if (instanceContainer == null) Interlocked.CompareExchange(ref instanceContainer, ServiceInstanceContainer.Create<DFLT>(localServiceInstance ?? new DFLT()), null);
            instanceContainer.CreateLocalServiceInstance(localServiceInstance);
        }

        internal class ServiceInstanceContainer : IServiceInstanceContainer
        {
            T globalInstance;
            AsyncLocal<T> localInstance;
            Func<T> createInstance;

            private ServiceInstanceContainer()
            {                
            }

            public static ServiceInstanceContainer Create<REAL>(T serviceInstance = null) where REAL:T, new()
            {
                var container = new ServiceInstanceContainer() { createInstance = () => new REAL(), globalInstance = serviceInstance ?? new REAL() };                
                ServiceRegistry.RegisterService(container);
                return container;
            }
            public static ServiceInstanceContainer Create(Func<T> createInstance)
            {
                var container = new ServiceInstanceContainer() { createInstance = createInstance, globalInstance = createInstance() };
                ServiceRegistry.RegisterService(container);
                return container;
            }


            public T Instance
            {
                get => localInstance?.Value ?? globalInstance;                    
                set
                {
                    if (localInstance != null) localInstance.Value = value;
                    else globalInstance = value;
                }
            }

            public void CreateLocalServiceInstance(T localServiceInstance)
            {
                if (localInstance == null)
                {
                    Interlocked.CompareExchange(ref localInstance, new AsyncLocal<T>(), null);
                }
                localInstance.Value = localServiceInstance;
            }

            public void CreateLocalServiceInstance()
            {
                if (localInstance == null)
                {
                    Interlocked.CompareExchange(ref localInstance, new AsyncLocal<T>(), null);
                }
                localInstance.Value = createInstance();
            }

            public void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
            {
                if (useLocalInstanceAsGlobal) globalInstance = localInstance.Value;
                localInstance = null;
            }

            public Type ServiceType => typeof(T);
            object IServiceInstanceContainer.Instance => Instance;
        }
    }    
}
