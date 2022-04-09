using FeatureLoom.Helpers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Serialization
{
    public interface IDeserializer
    {
        bool TryDeserialize<T>(byte[] data, out T obj);
        bool TryDeserialize<T>(string data, out T obj);
        Task<AsyncOut<bool, T>> TryDeserializeFromStreamAsync<T>(Stream stream, CancellationToken cancellationToken = default);
#if NETSTANDARD2_1_OR_GREATER
        bool TryDeserialize<T>(ReadOnlySpan<char> data, out T obj);
        bool TryDeserialize<T>(ReadOnlySpan<byte> data, out T obj);        
#endif
    }
}