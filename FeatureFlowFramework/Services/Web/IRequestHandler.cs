using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Services.Web
{
    public interface IWebRequestHandler
    {
        string Route { get; }

        Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response);
    }

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
    }

    public interface IWebResponse
    {
        Task WriteAsync(string reply);

        Stream Stream { get; }
        HttpStatusCode StatusCode { set; get; }
        string ContentType { get; set; }

        void AddCookie(string key, string content, CookieOptions options = null);

        void DeleteCookie(string key);
    }
}