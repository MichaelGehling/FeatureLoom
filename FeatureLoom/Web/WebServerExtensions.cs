using System;
using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public static class WebServerExtensions
    {
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, IWebResponse, Task<bool>> handleAction) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction));
        public static void HandleRequests(this IWebServer webserver, string route, Func<string> handleAction) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, string> handleAction) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, IWebResponse, string> handleAction) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction));
        public static void HandleRequests(this IWebServer webserver, string route, Func<Task<string>> handleAction) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, Task<string>> handleAction) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction));
        public static void HandleRequests(this IWebServer webserver, string route, Func<IWebRequest, IWebResponse, Task<string>> handleAction) => webserver.AddRequestHandler(new SimpleWebRequestHandler(route, handleAction));
        public static Task Run(this IWebServer webserver, HttpEndpointConfig endpoint)
        {
            webserver.AddEndpoint(endpoint);
            return webserver.Run();            
        }

        public static Task Run(this IWebServer webserver, IPAddress address, int port) => webserver.Run(new HttpEndpointConfig(address, port));
        public static Task Run(this IWebServer webserver, IPAddress address, int port, string certificateName) => webserver.Run(new HttpEndpointConfig(address, port, certificateName));


    }
}