using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureLoom.TCP
{
    public interface IMessageStreamReader : IDisposable
    {
        Task<object> ReadMessage(Stream stream, CancellationToken cancellationToken);
    }
}