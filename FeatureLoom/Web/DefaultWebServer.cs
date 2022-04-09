using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class DefaultWebServer : IWebServer
    {
        public class Config : Configuration
        {
            public HttpEndpointConfig[] endpointConfigs = { new HttpEndpointConfig(IPAddress.Loopback, 5000) };
        }

        private Config config = new Config();
        private FeatureLock myLock = new FeatureLock();
        private bool running = false;
        private IWebHost webserver;
        private byte[] favicon;

        // Invert comparer, so that when one route is the start of the second, the second will come first, so the more specifc before the less specific 
        static readonly IComparer<string> routeComparer = new GenericComparer<string>((route1, route2) => -route1.CompareTo(route2));
        private SortedList<string, IWebRequestHandler> requestHandlers = new SortedList<string, IWebRequestHandler>(routeComparer);
        private List<IWebRequestInterceptor> requestInterceptors = new List<IWebRequestInterceptor>();
        private List<HttpEndpointConfig> endpoints = new List<HttpEndpointConfig>();

        public DefaultWebServer()
        {
            favicon = Resources.favicon;
            TryUpdateConfigAsync().WaitFor();
        }

        public async Task<bool> TryUpdateConfigAsync()
        {
            if (await config.TryUpdateFromStorageAsync(false))
            {
                bool wasRunning = running;
                if (wasRunning) Stop();
                foreach (var endpoint in config.endpointConfigs.EmptyIfNull())
                {                    
                    AddEndpoint(endpoint);

                }
                if (wasRunning) _ = Run();
                return true;
            }
            return false;
        }

        public bool Running => running;

        public void AddEndpoint(HttpEndpointConfig endpoint)
        {
            using (myLock.Lock())
            {                
                endpoints.Add(endpoint);
                if (running)
                {
                    Stop();
                    Run();
                }
            }
        }

        public void AddRequestHandler(IWebRequestHandler handler)
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new SortedList<string, IWebRequestHandler>(requestHandlers, routeComparer);
                requestHandlers.Add(handler.Route, handler);
                this.requestHandlers = requestHandlers;
            }
        }

        public void RemoveRequestHandler(IWebRequestHandler handler)
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new SortedList<string, IWebRequestHandler>(requestHandlers, routeComparer);
                requestHandlers.Remove(handler.Route);
                this.requestHandlers = requestHandlers;
            }
        }

        public void ClearRequestHandlers()
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new SortedList<string, IWebRequestHandler>(routeComparer);
                else requestHandlers.Clear();
                this.requestHandlers = requestHandlers;

            }
        }

        public void AddRequestInterceptor(IWebRequestInterceptor interceptor)
        {
            using (myLock.Lock())
            {
                var currentRequestInterceptors = this.requestInterceptors;
                if (running) currentRequestInterceptors = new List<IWebRequestInterceptor>(currentRequestInterceptors);
                currentRequestInterceptors.Add(interceptor);
                this.requestInterceptors = currentRequestInterceptors;
            }
        }

        public void RemoveRequestInterceptor(IWebRequestInterceptor interceptor)
        {
            using (myLock.Lock())
            {
                var currentRequestInterceptors = this.requestInterceptors;
                if (running) currentRequestInterceptors = new List<IWebRequestInterceptor>(currentRequestInterceptors);
                currentRequestInterceptors.Remove(interceptor);
                this.requestInterceptors = currentRequestInterceptors;
            }
        }

        public void ClearRequestInterceptors()
        {
            using (myLock.Lock())
            {
                var currentRequestInterceptors = this.requestInterceptors;
                if (running) currentRequestInterceptors = new List<IWebRequestInterceptor>();
                else currentRequestInterceptors.Clear();
                this.requestInterceptors = currentRequestInterceptors;

            }
        }

        public void ClearEndpoints()
        {
            using (myLock.Lock())
            {
                endpoints.Clear();
                if (running)
                {
                    Stop();
                    Run();
                }
            }
        }

        public void Stop()
        {
            if (webserver != null)
            {
                webserver.StopAsync().WaitFor();
            }
        }

        public async Task StopAsync()
        {
            if (webserver != null)
            {
                await webserver.StopAsync();
            }
        }

        public void SetIcon(byte[] favicon)
        {
            this.favicon = favicon;
        }

        public Task Run()
        {
            return Task.Run(() =>
            {
                running = true;

                this.webserver = new WebHostBuilder()
                .UseKestrel(ApplyEndpoints)
                .Configure(applicationBuilder =>
                {
                    applicationBuilder.Map("", _applicationBuilder =>
                    {
                        _applicationBuilder.Run(HandleWebRequest);
                    });
                })
                .Build();

                this.webserver.Run();
                this.running = false;
            });
        }

        private async Task HandleWebRequest(HttpContext context)
        {
            ContextWrapper contextWrapper = new ContextWrapper(context);
            IWebRequest request = contextWrapper;
            IWebResponse response = contextWrapper;

            try
            {
                string path = context.Request.Path;

                // Shortcut to deliver icon.
                // If icon needs to be handled dynamically in a handler, the shortcut can be skipped by setting the favicon to null.
                if (favicon != null && request.IsGet && path == "/favicon.ico")
                {
                    response.StatusCode = HttpStatusCode.OK;
                    await response.Stream.WriteAsync(this.favicon, 0, favicon.Length);                    
                    return; // icon was delivered, so finish request handling
                }

                var currentInterceptors = this.requestInterceptors; // take current reference to avoid change while execution.
                foreach (var interceptor in currentInterceptors)
                {
                    if (await interceptor.InterceptRequestAsync(request, response))
                    {                      
                        return; // request was intercepted, so finish request handling
                    }
                }

                var currentRequestHandlers = this.requestHandlers; // take current reference to avoid change while execution.
                foreach (var handler in currentRequestHandlers)
                {
                    if (path.StartsWith(handler.Key))
                    {
                        contextWrapper.SetRoute(handler.Key);
                        if (await handler.Value.HandleRequestAsync(request, response))
                        {
                            return; // request was handled, so finish request handling
                        }
                    }

                }

                // Request was not handled so NotFound StatusCode is set, but it has to be ensured that the response stream whas not already written.
                if (!response.ResponseSent)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                }
            }
            catch (WebResponseException e)
            {
                if (e.LogLevel.HasValue) Log.SendLogMessage(new LogMessage(e.LogLevel.Value, e.InternalMessage));
                response.StatusCode = e.StatusCode;
                if (!e.ResponseMessage.EmptyOrNull()) await response.WriteAsync(e.ResponseMessage);
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), "Web request failed with an exception!", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
        }

        private async void ApplyEndpoints(KestrelServerOptions options)
        {
            options.Limits.MaxRequestBodySize = null;
            options.AllowSynchronousIO = false;
            foreach (var endpoint in endpoints)
            {
                if (endpoint.address == null) continue;

                if (!endpoint.certificateName.EmptyOrNull())
                {
                    if ((await Storage.GetReader("certificate").TryReadAsync<X509Certificate2>(endpoint.certificateName)).Out(out X509Certificate2 certificate))
                    {
                        options.Listen(endpoint.address, endpoint.port, listenOptions =>
                        {
                            listenOptions.UseHttps(certificate);
                        });
                    }
                    else
                    {
                        Log.ERROR(this.GetHandle(), $"Certificate {endpoint.certificateName} could not be retreived! Endpoint was not established.");
                    }
                }
                else
                {
                    options.Listen(endpoint.address, endpoint.port);
                }
            }
        }

        private class ContextWrapper : IWebRequest, IWebResponse
        {
            private HttpContext context;
            private string route = "";
            private bool responseSent = false;

            public void SetRoute(string route) => this.route = route;

            public ContextWrapper(HttpContext context)
            {
                this.context = context;
            }

            string IWebRequest.BasePath => context.Request.PathBase + route;

            string IWebRequest.FullPath => context.Request.PathBase + context.Request.Path;

            string IWebRequest.RelativePath => context.Request.Path.ToString().Substring(route.Length);

            Stream IWebRequest.Stream => context.Request.Body;

            Stream IWebResponse.Stream => context.Response.Body;

            string IWebRequest.ContentType => context.Request.ContentType;

            string IWebResponse.ContentType { get => context.Response.ContentType; set => context.Response.ContentType = value; }

            bool IWebRequest.IsGet => HttpMethods.IsGet(context.Request.Method);

            bool IWebRequest.IsPost => HttpMethods.IsPost(context.Request.Method);

            bool IWebRequest.IsPut => HttpMethods.IsPut(context.Request.Method);

            bool IWebRequest.IsDelete => HttpMethods.IsDelete(context.Request.Method);

            bool IWebRequest.IsHead => HttpMethods.IsHead(context.Request.Method);

            string IWebRequest.Method => context.Request.Method;            

            HttpStatusCode IWebResponse.StatusCode { get => (HttpStatusCode)context.Response.StatusCode; set => context.Response.StatusCode = (int)value; }

            string IWebRequest.HostAddress
            {
                get
                {
                    return (context.Request.IsHttps ? "https://" : "http://") + context.Request.Host.Value;
                }
            }


            bool IWebResponse.ResponseSent => responseSent;


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
        }        
    }
}