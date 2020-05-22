using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public static class Factory
    {
        public static T Create<T>() where T: class, new()
        {
            return OverrideFactory<T>.Create() ?? new T();
        }

        public static T Create<T>(string factoryName) where T : class, new()
        {
            return OverrideFactory<T>.Create(factoryName) ?? new T();
        }

        public static T Create<T>(Func<T> defaultCreate) where T : class
        {
            return OverrideFactory<T>.Create() ?? defaultCreate();
        }

        public static T Create<T>(string factoryName, Func<T> defaultCreate) where T : class
        {
            return OverrideFactory<T>.Create(factoryName) ?? defaultCreate();
        }

        public static void SetupOverride<T>(Func<T> overrideCreate) where T : class
        {
            OverrideFactory<T>.Setup(overrideCreate);
        }

        public static void SetupOverride<T>(string factoryName, Func<T> overrideCreate) where T : class
        {
            OverrideFactory<T>.Setup(factoryName, overrideCreate);            
        }

        private static class OverrideFactory<T> where T: class
        {
            public class Context : IServiceContextData
            {
                public Func<T> create = null;
                public Dictionary<string, Func<T>> namedFactories = null;

                public IServiceContextData Copy()
                {
                    return new Context()
                    {
                        create = this.create,
                        namedFactories = namedFactories == null ? null : new Dictionary<string, Func<T>>(namedFactories)
                    };
                }
            };

            static LazySlim<ServiceContext<Context>> context;

            public static void Setup(Func<T> newCreate)
            {
                context.Obj.Data.create = newCreate;
            }

            public static T Create()
            {
                return context.ObjIfExists.Data.create?.Invoke();
            }

            public static void Setup(string factoryName, Func<T> newCreate)
            {
                if (context.Obj.Data.namedFactories == null) context.Obj.Data.namedFactories = new Dictionary<string, Func<T>>();
                context.Obj.Data.namedFactories[factoryName] = newCreate;
            }

            public static T Create(string factoryName)
            {
                Func<T> create = null;
                if (context.ObjIfExists.Data.namedFactories?.TryGetValue(factoryName, out create) ?? false) return create.Invoke();
                else return null;
            }
        }
    }
}
