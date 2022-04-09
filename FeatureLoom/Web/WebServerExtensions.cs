using System;
using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public static class WebServerExtensions
    {
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, IWebResponse, Task<bool>> handleAction, string permissionWildcard = null) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction, permissionWildcard));
        public static void HandleRequests(this IWebServer webserver, string route, Func<string> handleAction, string permissionWildcard = null) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction, permissionWildcard));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, string> handleAction, string permissionWildcard = null) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction, permissionWildcard));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, IWebResponse, string> handleAction, string permissionWildcard = null) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction, permissionWildcard));
        public static void HandleRequests(this IWebServer webserver, string route, Func<Task<string>> handleAction, string permissionWildcard = null) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction, permissionWildcard));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, Task<string>> handleAction, string permissionWildcard = null) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction, permissionWildcard));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, IWebResponse, Task<string>> handleAction, string permissionWildcard = null) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction, permissionWildcard));
        public static Task Run(this IWebServer webserver, HttpEndpointConfig endpoint)
        {
            webserver.AddEndpoint(endpoint);
            return webserver.Run();            
        }

        public static Task Run(this IWebServer webserver, IPAddress address, int port) => webserver.Run(new HttpEndpointConfig(address, port));
        public static Task Run(this IWebServer webserver, IPAddress address, int port, string certificateName) => webserver.Run(new HttpEndpointConfig(address, port, certificateName));

        public static void InterceptRequests(this IWebServer webserver, Func<IWebRequest, IWebResponse, Task<bool>> interceptRequest) => webserver.AddRequestInterceptor(new SimpleWebRequestInterceptor(interceptRequest));


    }
}