using FeatureFlowFramework.Helpers.Extensions;
using System.Text;

namespace FeatureFlowFramework.Services.Serialization
{
    public class GenericByteSerializedObject : ISerializedObject
    {
        byte[] data;
        ISerializer serializer;

        public GenericByteSerializedObject(byte[] data, ISerializer serializer)
        {
            this.data = data;
            this.serializer = serializer;
        }

        public byte[] AsBytes()
        {
            return data;
        }

        public string AsString()
        {
            return data.GetString(Encoding.UTF8);
        }

        public bool TryDeserialize<T>(out T obj)
        {
            return serializer.TryDeserialize(data, out obj);
        }
    }
}
