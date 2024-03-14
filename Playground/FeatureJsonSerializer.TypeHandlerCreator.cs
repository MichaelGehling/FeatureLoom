using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Playground.FeatureJsonSerializer;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        public abstract class GenericTypeHandlerCreator : ITypeHandlerCreator
        {
            public abstract bool SupportsType(Type type);

            protected virtual Type CastType(Type type) => type;

            protected virtual void CreateAndSetGenericTypeHandler<T>(ExtensionApi api, ICachedTypeHandler cachedTypeHandler)
            {
                throw new NotImplementedException();
            }

            protected virtual void CreateAndSetGenericTypeHandler<T, ARG1>(ExtensionApi api, ICachedTypeHandler cachedTypeHandler)
            {
                throw new NotImplementedException();
            }

            protected virtual void CreateAndSetGenericTypeHandler<T, ARG1, ARG2>(ExtensionApi api, ICachedTypeHandler cachedTypeHandler)
            {
                throw new NotImplementedException();
            }

            protected virtual void CreateAndSetGenericTypeHandler<T, ARG1, ARG2, ARG3>(ExtensionApi api, ICachedTypeHandler cachedTypeHandler)
            {
                throw new NotImplementedException();
            }            

            public void CreateTypeHandler(ExtensionApi api, ICachedTypeHandler cachedTypeHandler, Type type)
            {
                type = CastType(type);
                List<Type> genericArguments = new List<Type> { type };
                genericArguments.AddRange(type.GenericTypeArguments);
                var createMethod = this.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(method => method.Name == nameof(CreateAndSetGenericTypeHandler) && method.GetGenericArguments().Length == genericArguments.Count);
                if (createMethod == null) throw new NotSupportedException();
                var genericCreateMethod = createMethod.MakeGenericMethod(genericArguments.ToArray());
                genericCreateMethod.Invoke(this, new object[] { api, cachedTypeHandler });
            }            
        }

        public class TypeHandlerCreator<T> : ITypeHandlerCreator
        {
            JsonDataTypeCategory category;
            Func<ExtensionApi, ItemHandler<T>> creator;
            bool onlyExactType;
            Func<Type, bool> supports;


            public TypeHandlerCreator(JsonDataTypeCategory category, Func<ExtensionApi, ItemHandler<T>> creator, bool onlyExactType = true)
            {
                this.category = category;
                this.creator = creator;
                this.onlyExactType = onlyExactType;
            }

            public TypeHandlerCreator(JsonDataTypeCategory category, Func<ExtensionApi, ItemHandler<T>> creator, Func<Type, bool> supportsType)
            {
                this.category = category;
                this.creator = creator;
                this.onlyExactType = false;
                this.supports = supportsType;
            }

            public void CreateTypeHandler(ExtensionApi api, ICachedTypeHandler cachedTypeHandler, Type _)
            {
                var itemHandler = creator.Invoke(api);
                cachedTypeHandler.SetItemHandler(itemHandler, category);
            }

            public bool SupportsType(Type type)
            {
                if (supports != null) return supports.Invoke(type);
                if (onlyExactType) return typeof(T) == type;
                return type.IsAssignableTo(typeof(T));
            }
        }

    }
}
