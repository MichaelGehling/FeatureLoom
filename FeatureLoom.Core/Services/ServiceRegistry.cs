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

        internal static void RegisterService(IServiceInstanceContainer service)
        {
            using (registryLock.Lock())
            {
                registry[service.ServiceType] = service;
            }
        }                

        public static IServiceInstanceContainer[] GetLocalServices()
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
                foreach(var service in registry.Values)
                {
                    service.CreateLocalServiceInstance();
                }
            }
        }

        public static void ClearAllLocalServiceInstances(bool useLocalInstanceAsGlobal)
        {
            using (registryLock.Lock())
            {
                foreach (var service in registry.Values)
                {
                    service.ClearAllLocalServiceInstances(useLocalInstanceAsGlobal);
                }
            }
        }
    }
}