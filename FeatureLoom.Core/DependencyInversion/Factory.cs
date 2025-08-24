using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace FeatureLoom.DependencyInversion
{
    /// <summary>
    /// Provides flexible, thread-safe factory methods for creating instances of types.
    /// Supports parameterless construction, custom creation overrides, and type-based instantiation.
    /// </summary>
    public static class Factory
    {
        /// <summary>
        /// Tries to create an instance of the specified type using a parameterless constructor or a registered override.
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <param name="instance">The created instance, or null if creation failed.</param>
        /// <returns>True if the instance was created successfully; otherwise, false.</returns>
        public static bool TryCreateFromType(Type type, out object instance)
        {
            instance = default;
            if (type == null) return false;
            return Service<TypeCreationCache>.Instance.TryCreate(type, out instance);
        }

        /// <summary>
        /// Tries to create an instance of the specified type, given by a simplified type name.
        /// </summary>
        /// <param name="simplifiedTypeName">The simplified type name.</param>
        /// <param name="instance">The created instance, or null if creation failed.</param>
        /// <returns>True if the instance was created successfully; otherwise, false.</returns>
        public static bool TryCreateFromType(string simplifiedTypeName, out object instance)
        {
            var type = TypeNameHelper.Shared.GetTypeFromSimplifiedName(simplifiedTypeName);
            return TryCreateFromType(type, out instance);
        }

        /// <summary>
        /// Creates an instance of type <typeparamref name="T"/> using a parameterless constructor or a registered override.
        /// </summary>
        /// <typeparam name="T">The type to instantiate.</typeparam>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        public static T Create<T>() where T : new()
        {
            if (Service<FactoryOverride<T>>.IsInitialized && Service<FactoryOverride<T>>.Instance.TryCreate(out var item)) return item;
            else return new T();            
        }

        /// <summary>
        /// Creates an instance of type <typeparamref name="T"/> using a registered override or the provided default creation delegate.
        /// </summary>
        /// <typeparam name="T">The type to instantiate.</typeparam>
        /// <param name="defaultCreate">The default creation delegate to use if no override is registered.</param>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        public static T Create<T>(Func<T> defaultCreate)
        {
            if (Service<FactoryOverride<T>>.IsInitialized && Service<FactoryOverride<T>>.Instance.TryCreate(out var item)) return item;
            else return defaultCreate();
        }

        /// <summary>
        /// Registers or updates a factory override for type <typeparamref name="T"/>.
        /// If <paramref name="overrideCreate"/> is null, removes the override.
        /// </summary>
        /// <typeparam name="T">The type to override creation for.</typeparam>
        /// <param name="overrideCreate">The override creation delegate.</param>
        public static void Override<T>(Func<T> overrideCreate)
        {
            if (overrideCreate != null) 
            {
                if (!Service<FactoryOverride<T>>.IsInitialized) Service<FactoryOverride<T>>.Init(_ => new FactoryOverride<T>(overrideCreate));
                else Service<FactoryOverride<T>>.Instance.Reset(overrideCreate);
            }
            else RemoveOverride<T>();
        }

        /// <summary>
        /// Removes the factory override for type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to remove the override for.</typeparam>
        public static void RemoveOverride<T>()
        {
            Service<FactoryOverride<T>>.Instance = FactoryOverride<T>.EmptyOverride;
            Service<TypeCreationCache>.Instance.RemoveOverride(typeof(T));
        }

        /// <summary>
        /// Holds a factory override for a specific type <typeparamref name="T"/>.
        /// </summary>
        private class FactoryOverride<T>
        {
            Func<T> create = null;
            static FactoryOverride<T> empty = new FactoryOverride<T>(null);
            public static FactoryOverride<T> EmptyOverride => empty;

            /// <summary>
            /// Initializes a new factory override with the specified creation delegate.
            /// </summary>
            /// <param name="createAction">The creation delegate.</param>
            public FactoryOverride(Func<T> createAction)
            {
                this.create = createAction;
                Service<TypeCreationCache>.Instance.OverrideCreateMethod(typeof(T), () => this.create());
            }

            /// <summary>
            /// Updates the creation delegate for this override.
            /// </summary>
            /// <param name="createAction">The new creation delegate.</param>
            public void Reset(Func<T> createAction)
            {
                this.create = createAction;
            }

            /// <summary>
            /// Tries to create an instance using the override delegate.
            /// </summary>
            /// <param name="item">The created instance, or default if creation failed.</param>
            /// <returns>True if creation succeeded; otherwise, false.</returns>
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

        /// <summary>
        /// Caches compiled constructors and custom creation delegates for types.
        /// Thread-safe.
        /// </summary>
        private class TypeCreationCache
        {
            Dictionary<Type, Func<object>> cache = new Dictionary<Type, Func<object>>();
            FeatureLock cacheLock = new FeatureLock();

            /// <summary>
            /// Tries to create an instance of the specified type using a cached or compiled parameterless constructor or override.
            /// </summary>
            /// <param name="type">The type to instantiate.</param>
            /// <param name="instance">The created instance, or null if creation failed.</param>
            /// <returns>True if the instance was created successfully; otherwise, false.</returns>
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

            /// <summary>
            /// Overrides the creation method for a specific type.
            /// </summary>
            /// <param name="type">The type to override.</param>
            /// <param name="createInstance">The creation delegate.</param>
            public void OverrideCreateMethod(Type type, Func<object> createInstance)
            {
                using (cacheLock.Lock())
                {
                    cache[type] = createInstance;
                }
            }

            /// <summary>
            /// Removes the override (and cached constructor) for a specific type.
            /// </summary>
            /// <param name="type">The type to remove from the cache.</param>
            public void RemoveOverride(Type type)
            {
                using (cacheLock.Lock())
                {
                    cache.Remove(type);
                }
            }
        }
    }
}