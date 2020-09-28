using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Services
{
    public static class Factory
    {
        public static T Create<T>() where T: new()
        {
            if (OverrideFactory<T>.Create(out T value)) return value;
            else return new T();
        }

        public static T Create<T>(string factoryName) where T : new()
        {
            if (OverrideFactory<T>.Create(factoryName, out T value)) return value;
            else return new T();
        }

        public static T Create<T>(Func<T> defaultCreate)
        {
            if (OverrideFactory<T>.Create(out T value)) return value;
            else return defaultCreate();
        }

        public static T Create<T>(string factoryName, Func<T> defaultCreate)
        {
            if (OverrideFactory<T>.Create(factoryName, out T value)) return value;
            else return defaultCreate();
        }

        public static void SetupOverride<T>(Func<T> overrideCreate)
        {
            OverrideFactory<T>.Setup(overrideCreate);
        }

        public static void SetupOverride<T>(string factoryName, Func<T> overrideCreate)
        {
            OverrideFactory<T>.Setup(factoryName, overrideCreate);            
        }

        private static class OverrideFactory<T>
        {
            public class ContextData : IServiceContextData
            {
                public Func<T> create = null;
                public Dictionary<string, Func<T>> namedFactories = null;
                public FastSpinLock myLock = new FastSpinLock();

                public IServiceContextData Copy()
                {
                    using(myLock.Lock())
                    {
                        return new ContextData()
                        {
                            create = this.create,
                            namedFactories = namedFactories == null ? null : new Dictionary<string, Func<T>>(namedFactories)
                        };
                    }
                }
            };

            static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

            public static void Setup(Func<T> newCreate)
            {
                context.Data.create = newCreate;
            }

            public static bool Create(out T value)
            {
                value = default;
                if (context.Data.create == null) return false;

                value = context.Data.create();
                return true;
            }

            public static void Setup(string factoryName, Func<T> newCreate)
            {
                using(context.Data.myLock.Lock())
                {
                    if(context.Data.namedFactories == null) context.Data.namedFactories = new Dictionary<string, Func<T>>();
                    context.Data.namedFactories[factoryName] = newCreate;
                }
            }

            public static bool Create(string factoryName, out T value)
            {
                value = default;
                using(context.Data.myLock.Lock())
                {
                    if(context.Data.namedFactories == null ||
                    !context.Data.namedFactories.TryGetValue(factoryName, out Func<T> create)) return false;

                    value = create();
                }
                return true;
            }
        }
    }
}
