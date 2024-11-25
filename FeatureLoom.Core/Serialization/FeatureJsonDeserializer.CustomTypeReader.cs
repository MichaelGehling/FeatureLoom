using System;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{    
    public interface ICustomTypeReader<T>
    {
        JsonDataTypeCategory JsonTypeCategory { get; }
        T ReadValue(ExtensionApi api);
    }

    public class CustomTypeReader<T> : ICustomTypeReader<T>
    {
        private JsonDataTypeCategory category;
        Func<ExtensionApi, T> readType;

        public CustomTypeReader(JsonDataTypeCategory category, Func<ExtensionApi, T> readType)
        {
            this.category = category;
            this.readType = readType;
        }

        public JsonDataTypeCategory JsonTypeCategory => category;

        public T ReadValue(ExtensionApi api) => readType(api);        
    }
}
