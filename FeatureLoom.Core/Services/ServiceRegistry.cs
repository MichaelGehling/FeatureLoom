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
        static HashSet<Type> declaredServiceTypes = new HashSet<Type>();
        static MicroLock registryLock = new MicroLock();
        static MicroLock serviceTypeLock = new MicroLock();
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

        public static void DeclareServiceType(Type declaredServiceType)
        {
            using(serviceTypeLock.Lock())
            {
                declaredServiceTypes.Add(declaredServiceType);
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

        internal static bool TryGetDefaultServiceCreator<T>(out Func<T> createServiceAction, bool tryBorrow = true)
        {
            if (tryBorrow)
            {
                using (registryLock.Lock())
                {
                    Type newType = typeof(T);
                    foreach (var service in registry.Values)
                    {
                        if (service.TryGetCreateServiceAction(out createServiceAction)) return true;
                    }
                }
            }
            var tType = typeof(T);

            var constructor = tType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                using (serviceTypeLock.Lock())
                {
                    var alternativeType = declaredServiceTypes.FirstOrDefault(p => tType.IsAssignableFrom(p) && p.GetConstructor(Type.EmptyTypes) != null);
                    constructor = alternativeType?.GetConstructor(Type.EmptyTypes);
                }                
            }
            if (constructor == null)
            {
                var alternativeType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes())
                    .FirstOrDefault(p => tType.IsAssignableFrom(p) && p.GetConstructor(Type.EmptyTypes) != null);
                constructor = alternativeType?.GetConstructor(Type.EmptyTypes);
            }

            if (constructor != null)
            {
                createServiceAction = () => (T) constructor.Invoke(Array.Empty<object>());
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