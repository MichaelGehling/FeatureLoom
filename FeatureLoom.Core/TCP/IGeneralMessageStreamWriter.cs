using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureLoom.TCP
{
    public interface IGeneralMessageStreamWriter : IDisposable
    {
        Task WriteMessage<T>(T message, Stream stream, CancellationToken cancellationToken);
    }
}