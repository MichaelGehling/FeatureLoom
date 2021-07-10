using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Helpers
{
    public static class Factory
    {
        public static T Get<T>(bool shareInstance = false) where T : new()
        {
            if (OverrideFactory<T>.TryCreate(out T value, shareInstance)) return value;
            else
            {
                T newInstance =  new T();
                if (shareInstance) OverrideFactory<T>.SetupSharedInstance(newInstance);
                return newInstance;
            }
        }

        public static T Get<T>(string factoryName, bool shareInstance = false) where T : new()
        {
            if (OverrideFactory<T>.TryCreate(factoryName, out T value, shareInstance)) return value;
            else
            {
                T newInstance = new T();
                if (shareInstance) OverrideFactory<T>.SetupSharedInstance(factoryName, newInstance);
                return newInstance;
            }
        }

        public static T Get<T>(Func<T> defaultCreate, bool shareInstance = false)
        {
            if (OverrideFactory<T>.TryCreate(out T value, shareInstance)) return value;
            else
            {
                T newInstance = defaultCreate();
                if (shareInstance) OverrideFactory<T>.SetupSharedInstance(newInstance);
                return newInstance;
            }
        }

        public static T Get<T>(string factoryName, Func<T> defaultCreate, bool shareInstance = false)
        {
            if (OverrideFactory<T>.TryCreate(factoryName, out T value, shareInstance)) return value;
            else
            {
                T newInstance = defaultCreate();
                if (shareInstance) OverrideFactory<T>.SetupSharedInstance(factoryName, newInstance);
                return newInstance;
            }
        }

        public static void PrepareCreate<T>(Func<T> create)
        {
            OverrideFactory<T>.SetupCreate(create);
        }

        public static void PrepareCreate<T>(string factoryName, Func<T> create)
        {
            OverrideFactory<T>.SetupCreate(factoryName, create);
        }

        public static void PrepareSharedInstance<T>(T instance)
        {
            OverrideFactory<T>.SetupSharedInstance(instance);
        }

        public static void PrepareSharedInstance<T>(string factoryName, T instance)
        {
            OverrideFactory<T>.SetupSharedInstance(factoryName, instance);
        }

        private static class OverrideFactory<T>
        {
            public class ContextData : IServiceContextData
            {
                public Func<T> create = null;
                public LazyValue<Dictionary<string, Func<T>>> namedFactories;
                public T defaultSharedInstance;
                public bool defaultSharedInstanceValid = false;
                public LazyValue<Dictionary<string, T>> namedSharedInstances;
                public MicroLock myLock = new MicroLock();

                public IServiceContextData Copy()
                {
                    using (myLock.Lock())
                    {
                        return new ContextData()
                        {
                            create = this.create,
                            defaultSharedInstance = defaultSharedInstance,
                            defaultSharedInstanceValid = defaultSharedInstanceValid,
                            namedFactories = namedFactories.Exists ? new LazyValue<Dictionary<string, Func<T>>>(namedFactories.Obj) : new LazyValue<Dictionary<string, Func<T>>>(),
                            namedSharedInstances = namedSharedInstances.Exists ? new LazyValue<Dictionary<string, T>>(namedSharedInstances.Obj) : new LazyValue<Dictionary<string, T>>()
                        };
                    }
                }
            };

            private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

            public static void SetupCreate(Func<T> newCreate)
            {
                context.Data.create = newCreate;
            }

            public static void SetupSharedInstance(T sharedInstance)
            {
                var data = context.Data;
                data.defaultSharedInstance = sharedInstance;
                data.defaultSharedInstanceValid = true;
            }

            public static bool TryCreate(out T value, bool shareInstance)
            {
                var data = context.Data;
                if (shareInstance && data.defaultSharedInstanceValid)
                {
                    value = data.defaultSharedInstance;
                    return true;
                }

                if (data.create != null)
                {
                    value = data.create();
                    if (shareInstance)
                    {
                        data.defaultSharedInstance = value;
                        data.defaultSharedInstanceValid = true;
                    }
                    return true;
                }

                value = default;
                return false;                
            }

            public static void SetupCreate(string factoryName, Func<T> newCreate)
            {
                using (context.Data.myLock.Lock())
                {
                    context.Data.namedFactories.Obj[factoryName] = newCreate;
                }
            }

            public static void SetupSharedInstance(string factoryName, T sharedInstance)
            {
                using (context.Data.myLock.Lock())
                {
                    context.Data.namedSharedInstances.Obj[factoryName] = sharedInstance;
                }
            }

            public static bool TryCreate(string factoryName, out T value, bool shareInstance)
            {
                var data = context.Data;
                using (data.myLock.Lock())
                {
                    if (data.namedSharedInstances.Exists && data.namedSharedInstances.Obj.TryGetValue(factoryName, out value))
                    {
                        return true;
                    }

                    if (data.namedFactories.Exists && data.namedFactories.Obj.TryGetValue(factoryName, out Func<T> create))
                    {
                        value = create();
                        data.namedSharedInstances.Obj[factoryName] = value;
                        return true;
                    }                    
                }
                value = default;
                return false;
            }
        }
    }
}