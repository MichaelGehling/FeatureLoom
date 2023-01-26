using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FeatureLoom.TCP
{
    public interface IMessageStreamWriter : IDisposable
    {
        Task WriteMessage(object message, Stream stream, CancellationToken cancellationToken);
    }
}