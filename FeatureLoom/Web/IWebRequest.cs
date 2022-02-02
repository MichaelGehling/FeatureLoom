using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebRequest
    {
        Stream Stream { get; }

        Task<string> ReadAsync();

        string ContentType { get; }

        bool TryGetCookie(string key, out string content);

        bool IsGet { get; }
        bool IsPost { get; }
        bool IsPut { get; }
        bool IsDelete { get; }
        bool IsHead { get; }
        string Method { get; }
        string RelativePath { get; }
        string BasePath { get; }
        string FullPath { get; }
        string HostAddress { get; }

        bool TryGetQueryItem(string key, out string item);
        IEnumerable<string> GetAllQueryKeys();
    }
}