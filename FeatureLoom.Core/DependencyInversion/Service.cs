using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FeatureLoom.DependencyInversion
{    
    public static partial class Service<T> where T:class
    {
        private static ServiceInstanceContainer unnamedInstanceContainer;
        private static Dictionary<string, ServiceInstanceContainer> namedInstanceContainers = new Dictionary<string, ServiceInstanceContainer>();
        private static MicroLock modifyLock = new MicroLock();
        private static bool initialized = false;

        public static void Init(Func<string, T> createServiceAction, bool forceReset = true)
        {
            if (forceReset) Reset();
            ServiceRegistry.RegisterCreator(new ServiceInstanceCreator(createServiceAction));
            initialized = true;
        }

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

        public static bool IsInitialized
        { 
            get 
            {
                if (!initialized) initialized = ServiceRegistry.TryGetServiceInstanceCreator<T>(out _);
                return initialized; 
            } 
        }

        public static T Get()
        {
            if (unnamedInstanceContainer != null) return unnamedInstanceContainer.Instance;
            return HandleUninitializedGet("");
        }

        public static void Set(T serviceInstance)
        {
            if (unnamedInstanceContainer != null) unnamedInstanceContainer.Instance = serviceInstance;
            else HandleUninitializedSet(serviceInstance, "");
        }

        public static T Get(string serviceInstanceName)
        {
            if (serviceInstanceName.EmptyOrNull()) return Get();

            if (namedInstanceContainers.TryGetValue(serviceInstanceName, out ServiceInstanceContainer instanceContainer)) return instanceContainer.Instance;
            return HandleUninitializedGet(serviceInstanceName);
        }

        public static void Set(string serviceInstanceName, T serviceInstance)
        {
            if (serviceInstanceName.EmptyOrNull())
            {
                Set(serviceInstance);
                return;
            }

            if (namedInstanceContainers.TryGetValue(serviceInstanceName, out ServiceInstanceContainer instanceContainer)) instanceContainer.Instance = serviceInstance;
            else HandleUninitializedSet(serviceInstance, serviceInstanceName);
        }

        public static T Instance
        {
            get => Get();
            set => Set(value);
        }

        public static void CreateLocalServiceInstance(T localServiceInstance = null) => CreateLocalServiceInstance("", localServiceInstance);
        public static void CreateLocalServiceInstance(string serviceInstanceName, T localServiceInstance = null)
        {
            if (serviceInstanceName == "")
            {
                if (unnamedInstanceContainer == null)
                {
                    if (!ServiceRegistry.TryGetServiceInstanceContainer(serviceInstanceName, out ServiceInstanceContainer newContainer)) throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
                    unnamedInstanceContainer = newContainer;
                }
                unnamedInstanceContainer.CreateLocalServiceInstance(localServiceInstance);
            }
            else
            {
                if(!namedInstanceContainers.TryGetValue(serviceInstanceName, out ServiceInstanceContainer instanceContainer))
                {
                    using(modifyLock.Lock())
                    {
                        if (!ServiceRegistry.TryGetServiceInstanceContainer<T>(serviceInstanceName, out instanceContainer)) throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
                        namedInstanceContainers.Add(serviceInstanceName, instanceContainer);
                    }
                }
                instanceContainer.CreateLocalServiceInstance(localServiceInstance);
            }
        }             

        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            if (unnamedInstanceContainer != null) unnamedInstanceContainer.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
            var namedInstanceContainersCopy = namedInstanceContainers;
            foreach (var container in namedInstanceContainersCopy.Values) container.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
        }

        private static void HandleUninitializedSet(T value, string serviceInstanceName)
        {
            if (value == null) return;
            if (!ServiceRegistry.TryGetServiceInstanceCreatorFromType(value.GetType(), out IServiceInstanceCreator creator)) throw new Exception($"Cannot create Service from instance type {value.GetType()}. Please initialize it first.");
            ServiceInstanceContainer newContainer = new ServiceInstanceContainer(creator, serviceInstanceName);
            newContainer.Instance = value;
            ServiceRegistry.RegisterService(newContainer);

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

        private static T HandleUninitializedGet(string serviceInstanceName)
        {
            if (!ServiceRegistry.TryGetServiceInstanceContainer<T>(serviceInstanceName, out ServiceInstanceContainer newContainer)) throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
            if (serviceInstanceName == "")
            {
                unnamedInstanceContainer = newContainer;
            }
            else using(modifyLock.Lock())
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
