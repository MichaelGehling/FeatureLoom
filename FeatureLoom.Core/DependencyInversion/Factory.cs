using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace FeatureLoom.DependencyInversion
{
    public static class Factory
    {
        public static bool TryCreateFromType(Type type, out object instance)
        {
            instance = default;
            if (type == null) return false;
            return Service<TypeCreationCache>.Instance.TryCreate(type, out instance);
        }

        public static bool TryCreateFromType(string simplifiedTypeName, out object instance)
        {
            var type = TypeHelper.GetTypeFromSimplifiedName(simplifiedTypeName);
            return TryCreateFromType(type, out instance);
        }

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
                if (!Service<FactoryOverride<T>>.IsInitialized) Service<FactoryOverride<T>>.Init(_ => new FactoryOverride<T>(overrideCreate));
                else Service<FactoryOverride<T>>.Instance.Reset(overrideCreate);
            }
            else RemoveOverride<T>();
        }

        public static void RemoveOverride<T>()
        {
            Service<FactoryOverride<T>>.Instance = FactoryOverride<T>.EmptyOverride;
        }

        private class FactoryOverride<T>
        {
            Func<T> create = null;
            static FactoryOverride<T> empty = new FactoryOverride<T>(null);
            public static FactoryOverride<T> EmptyOverride => empty;

            public FactoryOverride(Func<T> createAction)
            {
                this.create = createAction;
                Service<TypeCreationCache>.Instance.OverrideCreateMethod(typeof(T), () => this.create());
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

        private class TypeCreationCache
        {
            Dictionary<Type, Func<object>> cache = new Dictionary<Type, Func<object>>();
            FeatureLock cacheLock = new FeatureLock();

            public bool TryCreate(Type type, out object instance)
            {
                instance = default;
                using(var handle = cacheLock.LockReadOnly())
                {
                    if (!cache.TryGetValue(type, out var createInstance))
                    {
                        handle.UpgradeToWriteMode();

                        var constructorInfo = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                        if (constructorInfo == null) createInstance = null;
                        else
                        {
                            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(constructorInfo), typeof(object)));
                            createInstance = lambda.Compile();
                        }
                        cache[type] = createInstance;
                    }
                    if (createInstance == null) return false;
                    instance = createInstance();
                    return true;
                }
            }

            public void OverrideCreateMethod(Type type, Func<object> createInstance)
            {
                using (var handle = cacheLock.Lock())
                {
                    cache[type] = createInstance;
                }
            }
        }


    }
}