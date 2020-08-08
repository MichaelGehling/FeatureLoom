using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helpers.Misc
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

                public IServiceContextData Copy()
                {
                    return new ContextData()
                    {
                        create = this.create,
                        namedFactories = namedFactories == null ? null : new Dictionary<string, Func<T>>(namedFactories)
                    };
                }
            };

            static LazySlim<ServiceContext<ContextData>> context;

            public static void Setup(Func<T> newCreate)
            {
                context.Obj.Data.create = newCreate;
            }

            public static bool Create(out T value)
            {
                value = default;
                if (!context.IsInstantiated || context.Obj.Data.create == null) return false;

                value = context.Obj.Data.create();
                return true;
            }

            public static void Setup(string factoryName, Func<T> newCreate)
            {
                if (context.Obj.Data.namedFactories == null) context.Obj.Data.namedFactories = new Dictionary<string, Func<T>>();
                context.Obj.Data.namedFactories[factoryName] = newCreate;
            }

            public static bool Create(string factoryName, out T value)
            {                
                Func<T> create = null;
                value = default;
                if (!context.IsInstantiated || 
                    context.ObjIfExists.Data.namedFactories == null || 
                    !context.ObjIfExists.Data.namedFactories.TryGetValue(factoryName, out create)) return false;

                value = create();
                return true;
            }
        }
    }
}
