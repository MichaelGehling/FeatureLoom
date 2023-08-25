using FeatureLoom.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public partial class DefaultWebServer
    {
        private class ContextWrapper : IWebRequest, IWebResponse
        {
            private HttpContext context;
            private string route = "";
            private string relativePath = null;
            private string basePath = null;
            private string fullPath = null;
            private string originalPath;

            private bool responseSent = false;
            private bool statusCodeSet = false;

            public ContextWrapper(HttpContext context)
            {
                this.context = context;
            }

            public void SetRoute(string route)
            {
                if (this.route != route)
                {
                    this.route = route;
                    relativePath = null;
                    basePath = null;
                }
            }

            private string UpdateRelativePath()
            {
                relativePath = context.Request.Path.ToString().Substring(route.Length);
                return relativePath;
            }

            private string UpdateBasePath()
            {
                basePath = context.Request.PathBase + route;
                return basePath;
            }

            private string UpdateFullPath()
            {
                fullPath = context.Request.PathBase + context.Request.Path;                
                return fullPath;
            }

            void IWebRequest.ChangePath(string newPath)
            {
                originalPath = context.Request.Path;
                context.Request.Path = newPath;
                relativePath = null;
                basePath = null;
                fullPath = null;
            }

            string IWebRequest.OriginalPath => originalPath ?? context.Request.Path;
            string IWebRequest.BasePath => basePath ?? UpdateBasePath();

            string IWebRequest.Path => context.Request.Path;

            string IWebRequest.FullPath => fullPath ?? UpdateFullPath();

            string IWebRequest.RelativePath => relativePath ?? UpdateRelativePath();

            Stream IWebRequest.Stream => context.Request.Body;            

            string IWebRequest.ContentType => context.Request.ContentType;            

            bool IWebRequest.IsGet => HttpMethods.IsGet(context.Request.Method);

            bool IWebRequest.IsPost => HttpMethods.IsPost(context.Request.Method);

            bool IWebRequest.IsPut => HttpMethods.IsPut(context.Request.Method);

            bool IWebRequest.IsDelete => HttpMethods.IsDelete(context.Request.Method);

            bool IWebRequest.IsHead => HttpMethods.IsHead(context.Request.Method);

            string IWebRequest.Method => context.Request.Method;
            string IWebRequest.HostAddress
            {
                get
                {
                    return (context.Request.IsHttps ? "https://" : "http://") + context.Request.Host.Value;
                }
            }

            Stream IWebResponse.Stream
            {
                get
                {
                    responseSent = true;
                    return context.Response.Body;
                }
            }
            string IWebResponse.ContentType { get => context.Response.ContentType; set => context.Response.ContentType = value; }

            HttpStatusCode IWebResponse.StatusCode 
            { 
                get => (HttpStatusCode)context.Response.StatusCode; 
                set
                { 
                    context.Response.StatusCode = (int)value; 
                    statusCodeSet = true; 
                } 
            }            


            bool IWebResponse.ResponseSent => responseSent;
            bool IWebResponse.StatusCodeSet => statusCodeSet;


            void IWebResponse.AddCookie(string key, string content, CookieOptions options)
            {
                if (options != null) context.Response.Cookies.Append(key, content, options);
                else context.Response.Cookies.Append(key, content);
            }

            void IWebResponse.DeleteCookie(string key)
            {
                context.Response.Cookies.Delete(key);
            }

            Task<string> IWebRequest.ReadAsync()
            {
                return context.Request.Body.ReadToStringAsync();
            }

            bool IWebRequest.TryGetCookie(string key, out string content)
            {
                return context.Request.Cookies.TryGetValue(key, out content);
            }

            bool IWebRequest.TryGetQueryItem(string key, out string item)
            {
                if (context.Request.Query.TryGetValue(key, out StringValues values))
                {
                    item = values[0];
                    return true;
                }
                else
                {
                    item = default;
                    return false;
                }
            }

            IEnumerable<string> IWebRequest.GetAllQueryKeys() => context.Request.Query.Keys;

            Task IWebResponse.WriteAsync(string reply)
            {
                responseSent = true;
                return context.Response.WriteAsync(reply);
            }

            HandlerResult IWebResponse.Redirect(string url)
            {
                context.Response.Redirect(url);
                responseSent = true;
                return new HandlerResult(true, null, HttpStatusCode.Redirect);
            }
        }        
    }
}