using FeatureLoom.TCP;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureLoom.TCP
{
    public interface ISpecificMessageStreamReader : IDisposable
    {
        bool CanRead(byte[] typeInfoBuffer, int typeInfoStartIndex, int typeInfoLength);
        Task<object> ReadMessage(Stream stream, int messageLength, CancellationToken cancellationToken);
    }
}
