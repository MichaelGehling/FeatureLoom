using FeatureFlowFramework.Helpers.Misc;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Services.Serialization
{
    public interface ISerializer
    {        
        bool TrySerialize<T>(T obj, out ISerializedObject serializedObject);        
        bool TryDeserialize<T>(byte[] data, out T obj);
        Task<bool> TrySerializeToStreamAsync<T>(T obj, Stream stream, CancellationToken cancellationToken = default);
        Task<AsyncOut<bool, T>> TryDeserializeFromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default);
        Task<AsyncOut<bool, ISerializedObject>> TryReadSerializedObjectFromStreamAsync(Stream stream, CancellationToken cancellationToken = default);
    }


}
