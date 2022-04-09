using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Services
{
    public static class Factory
    {
        public static T Create<T>() where T : new()
        {
            if (Service<FactoryOverride<T>>.IsInitialized && Service<FactoryOverride<T>>.Instance.TryCreate(out var item)) return item;
            else return new T();            
        }

        public static T Create<T>(Func<T> defaultCreate)
        {
            if (Service<FactoryOverride<T>>.IsInitialized && Service<FactoryOverride<T>>.Instance.TryCreate(out var item)) return item;
            else return defaultCreate();
        }

        public static void Override<T>(Func<T> overrideCreate)
        {
            if (overrideCreate != null) 
            {
                if (!Service<FactoryOverride<T>>.IsInitialized) Service<FactoryOverride<T>>.Init(() => new FactoryOverride<T>(overrideCreate));
                else Service<FactoryOverride<T>>.Instance.Reset(overrideCreate);
            }
            else RemoveOverride<T>();
        }

        public static void RemoveOverride<T>()
        {
            if (Service<FactoryOverride<T>>.UsesLocalInstance) Service<FactoryOverride<T>>.Instance = null;
            else Service<FactoryOverride<T>>.Reset();
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

            public bool TryCreate(out T item)
            {
                if (create != null)
                {
                    item = create();
                    return true;
                }
                else
                {
                    item = default;
                    return false;
                }
            }
        }
    }
}