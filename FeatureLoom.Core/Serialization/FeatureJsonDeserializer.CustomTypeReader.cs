using System;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{    
    public interface ICustomTypeReader<T>
    {
        JsonDataTypeCategory JsonTypeCategory { get; }
        StringRepresentation StringRepresentation { get; }
        T ReadValue(ExtensionApi api);
    }

    public class CustomTypeReader<T> : ICustomTypeReader<T>
    {
        private JsonDataTypeCategory category;
        private StringRepresentation stringRepresentation;
        Func<ExtensionApi, T> readType;

        public CustomTypeReader(JsonDataTypeCategory category, StringRepresentation stringRepresentation, Func<ExtensionApi, T> readType)
        {
            this.category = category;
            this.readType = readType;
            this.stringRepresentation = stringRepresentation;
        }

        public JsonDataTypeCategory JsonTypeCategory => category;

        public T ReadValue(ExtensionApi api) => readType(api);        
        public StringRepresentation StringRepresentation => stringRepresentation;
    }
}
