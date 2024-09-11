using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonSerializer
    {
        public interface ITypeHandlerCreator
        {
            bool SupportsType(Type type);
            void CreateTypeHandler(ExtensionApi api, ICachedTypeHandler cachedTypeHandler, Type type);
        }

        public abstract class GenericTypeHandlerCreator : ITypeHandlerCreator
        {
            protected Type genericType;
            public GenericTypeHandlerCreator(Type genericType)
            {
                this.genericType = genericType;
            }

            public virtual bool SupportsType(Type type)
            {
                return type.IsOfGenericType(genericType);
            }

            protected virtual void CreateAndSetGenericTypeHandler<ARG1>(ExtensionApi api, ICachedTypeHandler cachedTypeHandler)
            {
                throw new NotImplementedException();
            }

            protected virtual void CreateAndSetGenericTypeHandler<ARG1, ARG2>(ExtensionApi api, ICachedTypeHandler cachedTypeHandler)
            {
                throw new NotImplementedException();
            }

            protected virtual void CreateAndSetGenericTypeHandler<ARG1, ARG2, ARG3>(ExtensionApi api, ICachedTypeHandler cachedTypeHandler)
            {
                throw new NotImplementedException();
            }

            public void CreateTypeHandler(ExtensionApi api, ICachedTypeHandler cachedTypeHandler, Type type)
            {
                if (!type.IsOfGenericType(genericType, out Type concreteGenericType)) throw new NotSupportedException();
                this.InvokeGenericMethod(nameof(CreateAndSetGenericTypeHandler), concreteGenericType.GetGenericArguments(), api, cachedTypeHandler);
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
