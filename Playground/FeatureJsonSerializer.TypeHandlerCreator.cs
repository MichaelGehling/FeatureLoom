using System;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
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

            public void CreateTypeHandler(FeatureJsonSerializer.ExtensionApi api, FeatureJsonSerializer.ICachedTypeHandler cachedTypeHandler)
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
