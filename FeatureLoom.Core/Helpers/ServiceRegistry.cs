using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FeatureLoom.Helpers
{
    public static class ServiceRegistry
    {
        static List<IServiceContainer> serviceContainers = new List<IServiceContainer>();
        static FeatureLock serviceContainersLock = new FeatureLock();

        public static T GetServiceInstance<T>() where T : class
        {
            return ServiceContainer<T>.GetInstance();
        }

        public static void InitService<T>(Func<T> createServiceInstance) where T : class
        {
            if (ServiceContainer<T>.Init(createServiceInstance, out var serviceContainer))
            {
                using (serviceContainersLock.Lock()) serviceContainers.Add(serviceContainer);
            }
        }

        public static void CreateThreadLocalInstances()
        {
            using (serviceContainersLock.Lock())
            {
                foreach(var serviceContainer in serviceContainers)
                {
                    serviceContainer.CreateLocal();
                }
            }
        }

        public static void CreateThreadLocalInstance<T>() where T : class
        {
            ServiceContainer<T>.CreateLocalInstance();
        }

        public static void SetThreadLocalInstance<T>(T serviceInstance) where T : class
        {
            ServiceContainer<T>.SetLocalInstance(serviceInstance);
        }

        private interface IServiceContainer
        {
            void CreateLocal();
        }

        private class ServiceContainer<T> : IServiceContainer where T : class
        {
            static T globalInstance = null;
            static ThreadLocal<T> localInstance = null;
            static Func<T> createInstance = null;

            public static T GetInstance()
            {
                if (localInstance != null && localInstance.IsValueCreated) return localInstance.Value;
                else return globalInstance;
            }

            public static bool Init(Func<T> create, out ServiceContainer<T> serviceContainer)
            {
                if (null == Interlocked.CompareExchange(ref createInstance, create, null))
                {
                    globalInstance = create();
                    serviceContainer = new ServiceContainer<T>();
                    return true;
                }
                else
                {
                    serviceContainer = null;
                    return false;
                }
            }

            public void CreateLocal() => CreateLocalInstance();

            public static void CreateLocalInstance()
            {
                if (createInstance != null)
                {
                    if (null == Interlocked.CompareExchange(ref localInstance, new ThreadLocal<T>(), null))
                    {
                        localInstance.Value = createInstance();
                    }
                }
                else throw new Exception($"Service {typeof(T)} was not Initialized, yet.");
            }

            public static void SetLocalInstance(T serviceInstance)
            {                
                if (null == Interlocked.CompareExchange(ref localInstance, new ThreadLocal<T>(), null))
                {
                    localInstance.Value = serviceInstance;
                }                
            }
        }
    }    
}