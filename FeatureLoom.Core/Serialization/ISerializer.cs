using FeatureLoom.Helpers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Serialization
{
    public interface ISerializer
    {
        bool TrySerialize<T>(T obj, out ISerializedObject serializedObject);

        bool TryDeserialize<T>(byte[] data, out T obj);

        Task<bool> TrySerializeToStreamAsync<T>(T obj, Stream stream, CancellationToken cancellationToken = default);

        Task<AsyncOut<bool, T>> TryDeserializeFromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default);

        Task<AsyncOut<bool, ISerializedObject>> TryReadSerializedObjectFromStreamAsync(Stream stream, CancellationToken cancellationToken = default);

        /* DecodingStatus Decode<T>(Span<byte> data, out int processedBytes, out T decodedMessage, ref object decodingContextData);
         EncodingStatus Encode<T>(T obj, out Span<byte> data, ref object encodingContextData);
         bool TryCreateSerializedObject<T>(T obj, out ISerializedObject serializedObject);*/
    }

    /*
    public enum DecodingStatus
    {
        Failed,
        Complete,
        Incomplete
    }

    public enum EncodingStatus
    {
        Failed,
        Complete
    }
    */
}