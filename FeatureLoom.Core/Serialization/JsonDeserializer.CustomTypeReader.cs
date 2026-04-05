using System;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{    
    public interface ICustomTypeReader<T>
    {
        T ReadValue(ExtensionApi api);
    }

    public class CustomTypeReader<T> : ICustomTypeReader<T>
    {
        Func<ExtensionApi, T> readType;

        public CustomTypeReader(Func<ExtensionApi, T> readType)
        {
            this.readType = readType;
        }

        public T ReadValue(ExtensionApi api) => readType(api);        
    }
}
