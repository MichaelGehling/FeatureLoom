using FeatureFlowFramework.Helpers.Extensions;
using System.Text;

namespace FeatureFlowFramework.Services.Serialization
{
    public class GenericStringSerializedObject : ISerializedObject
    {
        string data;
        ISerializer serializer;

        public GenericStringSerializedObject(string data, ISerializer serializer)
        {
            this.data = data;
            this.serializer = serializer;
        }

        public byte[] AsBytes()
        {
            return data.ToByteArray(Encoding.UTF8);
        }

        public string AsString()
        {
            return data;
        }

        public bool TryDeserialize<T>(out T obj)
        {
            return serializer.TryDeserialize(data, out obj);
        }
    }
}
