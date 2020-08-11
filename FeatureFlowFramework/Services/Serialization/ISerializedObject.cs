using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Services.Serialization
{
    public interface ISerializedObject
    {
        byte[] AsBytes();
        bool TryDeserialize<T>(out T obj);
        Task<bool> TryWriteToStreamAsync(Stream stream, CancellationToken cancellationToken = default);
    }

}
