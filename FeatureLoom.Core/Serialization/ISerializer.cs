using FeatureLoom.Helpers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Serialization
{
    public interface ISerializer
    {
        bool TrySerialize<T>(T obj, out string data);
        bool TrySerialize<T>(T obj, out byte[] data);
        bool TrySerializeToStreamAsync<T>(T obj, Stream stream);
    }

}