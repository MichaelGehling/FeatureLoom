using FeatureLoom.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FeatureLoom.DependencyInversion
{
    public static partial class Service<T> where T:class
    {
        private static ServiceInstanceContainer instanceContainer;
        private static bool initialized = false;

        public static void Init(Func<string, T> createServiceAction, bool forceReset = true)
        {
            if (forceReset) Reset();
            ServiceRegistry.RegisterCreator(new ServiceInstanceCreator(createServiceAction));
            initialized = true;
        }

        public static void Reset()
        {
            if (instanceContainer != null)
            {
                ServiceRegistry.UnregisterService(instanceContainer);
                instanceContainer = null;
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
            if (instanceContainer != null) return instanceContainer.Instance;
            return HandleUninitializedGet();
        }

        public static void Set(T serviceInstance)
        {
            if (instanceContainer != null) instanceContainer.Instance = serviceInstance;
            else HandleUninitializedSet(serviceInstance);
        }

        public static T Instance
        {
            get => Get();
            set => Set(value);
        }

        public static void CreateLocalServiceInstance(T localServiceInstance = null)
        {
            if (instanceContainer == null)
            {
                if (!ServiceRegistry.TryGetServiceInstanceContainer(out ServiceInstanceContainer newContainer)) throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
                instanceContainer = newContainer;
            }            
            instanceContainer.CreateLocalServiceInstance(localServiceInstance);                
        }               

        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            if (instanceContainer != null) instanceContainer.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);            
        }

        private static void HandleUninitializedSet(T value)
        {            
            if (value == null) return;

            if (!ServiceRegistry.TryGetServiceInstanceCreatorFromType(value.GetType(), out IServiceInstanceCreator creator)) throw new Exception($"Cannot create Service from instance type {value.GetType()}. Please initialize it first.");
            ServiceInstanceContainer newContainer = new ServiceInstanceContainer(creator);
            newContainer.Instance = value;
            ServiceRegistry.RegisterService(newContainer);            
            instanceContainer = newContainer;
        }

        private static T HandleUninitializedGet()
        {
            if (!ServiceRegistry.TryGetServiceInstanceContainer<T>(out instanceContainer)) throw new Exception($"Cannot create Service of type {typeof(T)}. Please initialize it first.");
            return instanceContainer.Instance;
        }                      
    }    
}
