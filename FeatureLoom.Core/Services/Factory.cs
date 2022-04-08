using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Services
{
    public static class Factory
    {
        public static T Create<T>() where T : new()
        {
            if (Service<FactoryOverride<T>>.IsInitialized) return Service<FactoryOverride<T>>.Instance.Create(); 
            else return new T();            
        }

        public static T Create<T>(Func<T> defaultCreate)
        {
            if (Service<FactoryOverride<T>>.IsInitialized) return Service<FactoryOverride<T>>.Instance.Create();
            else return defaultCreate();
        }

        public static void OverrideCreate<T>(Func<T> overrideCreate)
        {
            if (!Service<FactoryOverride<T>>.IsInitialized) Service<FactoryOverride<T>>.Init(() => new FactoryOverride<T>(overrideCreate));
            else Service<FactoryOverride<T>>.Instance.Reset(overrideCreate);
        }        

        public class FactoryOverride<T>
        {
            Func<T> create = null;

            public FactoryOverride(Func<T> createAction)
            {
                this.create = createAction;
            }

            public void Reset(Func<T> createAction)
            {
                this.create = createAction;
            }

            public T Create() => create();
        }
    }
}