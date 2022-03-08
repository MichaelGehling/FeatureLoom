using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebResponse
    {
        Task WriteAsync(string reply);

        Stream Stream { get; }
        HttpStatusCode StatusCode { set; get; }
        string ContentType { get; set; }

        void AddCookie(string key, string content, CookieOptions options = null);

        void DeleteCookie(string key);

        bool ResponseSent { get; }
    }
}