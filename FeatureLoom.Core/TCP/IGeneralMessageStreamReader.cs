using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureLoom.TCP
{
    public interface IGeneralMessageStreamReader : IDisposable
    {
        Task<object> ReadMessage(Stream stream, CancellationToken cancellationToken);
    }
}