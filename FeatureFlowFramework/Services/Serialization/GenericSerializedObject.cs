using FeatureLoom.Helpers.Extensions;
using FeatureLoom.Services.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Services.Serialization
{
    public class GenericSerializedObject : ISerializedObject
    {
        byte[] data;
        ISerializer serializer;

        public GenericSerializedObject(byte[] data, ISerializer serializer)
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

        public async Task<bool> TryWriteToStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {                
                await stream.WriteAsync(data, 0, data.Length, cancellationToken);
                return true;
            }
            catch(Exception e)
            {
                Log.ERROR($"Writing serializedObject to stream failed!", e.ToString());
                return false;
            }
        }
    }
}
