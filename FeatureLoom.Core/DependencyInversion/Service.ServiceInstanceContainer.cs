using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
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
            IServiceInstanceCreator creator;
            string serviceInstanceName;
            MicroLock creationLock = new MicroLock();

            public ServiceInstanceContainer(IServiceInstanceCreator creator, string serviceInstanceName)
            {
                this.creator = creator;
                this.serviceInstanceName = serviceInstanceName;
            }

            internal ServiceInstanceContainer(IServiceInstanceContainer container, string serviceInstanceName)
            {
                this.serviceInstanceName = serviceInstanceName;

                if (!typeof(T).IsAssignableFrom(container.ServiceType)) throw new Exception("Incompatible ServiceInstanceContainer used!");
                creator = container.ServiceInstanceCreator;
                globalInstance = container.GlobalInstance as T;
                if (container.UsesLocalInstance) CreateLocalServiceInstance(container.Instance as T);
            }

            public string ServiceInstanceName => serviceInstanceName;

            public IServiceInstanceCreator ServiceInstanceCreator => creator;

            public bool UsesLocalInstance => localInstance.Exists;

            object IServiceInstanceContainer.GlobalInstance => globalInstance;

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

            public void CreateLocalServiceInstance(T localServiceInstance = null)
            {
                localInstance.Obj.Value = localServiceInstance ?? creator.CreateServiceInstance<T>();
            }

            public void CreateLocalServiceInstance()
            {
                localInstance.Obj.Value = creator.CreateServiceInstance<T>();
            }

            public void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
            {
                if (localInstance.Exists && useLocalInstanceAsGlobal) globalInstance = localInstance.Obj.Value;
                localInstance.RemoveObj();
            }            

            public Type ServiceType => typeof(T);
            public Type ServiceCreatorType => creator.ServiceType;
            object IServiceInstanceContainer.Instance => Instance;
        }
    }    
}
