using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FeatureLoom.Services
{
    public static partial class Service<T> where T:class
    {
        private static ServiceInstanceContainer instanceContainer;

        public static T Init(Func<T> createServiceAction, bool force = true)
        {
            if (force || instanceContainer == null)
            {
                Reset();
                Interlocked.CompareExchange(ref instanceContainer, ServiceInstanceContainer.Create(createServiceAction), null);
                if (ServiceRegistry.LocalInstancesForAllServicesActive) instanceContainer.CreateLocalServiceInstance();
            }
            return Instance;
        }

        public static void Reset()
        {
            if (instanceContainer != null)
            {
                ServiceRegistry.UnregisterService(instanceContainer);
                instanceContainer = null;
            }
        }

        public static bool IsInitialized => instanceContainer != null;
        public static bool UsesLocalInstance => IsInitialized && instanceContainer.UsesLocalInstance;

        public static T Instance
        {
            get
            {
                if (instanceContainer != null) return instanceContainer.Instance;
                else return HandleUninitializedGet();
            }

            set
            {
                if (instanceContainer != null) instanceContainer.Instance = value;
                else HandleUninitializedSet(value);
            }
        }
        
        public static void CreateLocalServiceInstance(T localServiceInstance = null)
        {
            if (instanceContainer != null) instanceContainer.CreateLocalServiceInstance(localServiceInstance);
            else if (ServiceRegistry.TryGetDefaultServiceCreator<T>(out var createServiceAction))
            {
                Init(createServiceAction);
                instanceContainer.CreateLocalServiceInstance(localServiceInstance);
            }
            else throw new Exception($"Service<{typeof(T)}> was not initialized.");
        }

        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            if (instanceContainer != null) instanceContainer.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
            else throw new Exception($"Service<{typeof(T)}> was not initialized.");
        }

        private static void HandleUninitializedSet(T value)
        {
            if (ServiceRegistry.TryGetDefaultServiceCreator<T>(out var createServiceAction))
            {
                Init(createServiceAction);
                instanceContainer.Instance = value;
            }
            else throw new Exception($"Service<{typeof(T)}> was not initialized.");
        }

        private static T HandleUninitializedGet()
        {
            foreach (var serviceContainer in ServiceRegistry.GetAllRegisteredServices())
            {
                if (serviceContainer.Instance is T borrowedInstance && serviceContainer.TryGetCreateServiceAction<T>(out Func<T> borrowedCreateServiceAction))
                {
                    if (null == Interlocked.CompareExchange(ref instanceContainer, ServiceInstanceContainer.Create(borrowedCreateServiceAction, borrowedInstance), null))
                    {                        
                        if (ServiceRegistry.LocalInstancesForAllServicesActive) instanceContainer.CreateLocalServiceInstance();                        
                    }
                    return instanceContainer.Instance;
                }
            }

            if (ServiceRegistry.TryGetDefaultServiceCreator<T>(out var createServiceAction, false))
            {
                Init(createServiceAction);
                return instanceContainer.Instance;
            }
            else throw new Exception($"Service<{typeof(T)}> was not initialized.");
        }                      
    }    
}
