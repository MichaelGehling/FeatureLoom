using System;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        public class TypeHandlerCreator<T> : ITypeHandlerCreator
        {
            JsonDataTypeCategory category;
            Func<ExtensionApi, ItemHandler<T>> creator;

            public TypeHandlerCreator(JsonDataTypeCategory category, Func<ExtensionApi, ItemHandler<T>> creator)
            {
                this.category = category;
                this.creator = creator;
            }

            public void CreateTypeHandler<T>(FeatureJsonSerializer.ExtensionApi api, FeatureJsonSerializer.ICachedTypeHandler cachedTypeHandler)
            {
                var itemHandler = creator.Invoke(api);
                cachedTypeHandler.SetItemHandler(itemHandler, category);
            }

            public bool SupportsType(Type type)
            {
                return typeof(T) == type;
            }
        }

    }
}
