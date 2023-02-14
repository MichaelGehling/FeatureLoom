using FeatureLoom.TCP;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureLoom.TCP
{
    public interface ISpecificMessageStreamWriter : IDisposable
    {
        bool CanWrite<T>(T message, out byte[] typeInfo);
        Task WriteMessage<T>(T message, Stream stream, CancellationToken cancellationToken);
    }
}
