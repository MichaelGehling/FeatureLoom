using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FeatureLoom.Services
{
    public static class ServiceRegistry
    {
        static Dictionary<Type, IServiceInstanceContainer> registry = new Dictionary<Type, IServiceInstanceContainer>();
        static FeatureLock registryLock = new FeatureLock();
        static bool localInstancesForAllServicesActive = false;

        public static bool LocalInstancesForAllServicesActive => localInstancesForAllServicesActive;

        internal static void RegisterService(IServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                registry[service.ServiceType] = service;
            }
        }

        internal static void UnregisterService(IServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                registry.Remove(service.ServiceType);
            }
        }

        public static IServiceInstanceContainer[] GetAllRegisteredServices()
        {
            using(registryLock.Lock())
            {
                return registry.Values.ToArray();
            }
        }

        public static void CreateLocalInstancesForAllServices()
        {
            using (registryLock.Lock())
            {
                localInstancesForAllServicesActive = true;
                foreach (var service in registry.Values)
                {
                    service.CreateLocalServiceInstance();
                }
            }
        }

        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            using (registryLock.Lock())
            {
                localInstancesForAllServicesActive = false;
                foreach (var service in registry.Values)
                {
                    service.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
                }
            }
        }

        internal static bool TryGetDefaultServiceCreator<T>(out Func<T> createServiceAction)
        {
            switch(typeof(T))
            {
                default:
                    {
                        var contructor = typeof(T).GetConstructor(Type.EmptyTypes);
                        if (contructor != null)
                        {
                            createServiceAction = () => (T) contructor.Invoke(Array.Empty<object>());
                            return true;
                        }
                        else
                        {
                            createServiceAction = null;
                            return false;
                        }
                    }
            }
        }

    }
}