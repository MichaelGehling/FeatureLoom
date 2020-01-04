using FeatureFlowFramework.DataStorage;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Web
{
    public class DefaultWebServer : IWebServer
    {
        public class Config : Configuration
        {
            public HttpEndpointConfig[] endpointConfigs = { new HttpEndpointConfig(IPAddress.Loopback, 5000) };
        }

        private Config config = new Config();
        private readonly object configLock = new object();
        public bool started = false;
        private IWebHost webserver;

        //public List<Action<IServiceCollection>> serviceExtensions = new List<Action<IServiceCollection>>();
        public SortedList<string, IWebRequestHandler> requestHandlers = new SortedList<string, IWebRequestHandler>();

        public List<HttpEndpointConfig> endpoints = new List<HttpEndpointConfig>();

        public DefaultWebServer()
        {
            TryUpdateConfig();
        }

        public bool TryUpdateConfig()
        {
            if (config.TryUpdateFromStorage(false))
            {
                foreach (var endpoint in config.endpointConfigs.EmptyIfNull())
                {
                    AddEndpoint(endpoint);
                }
                return true;
            }
            return false;
        }

        public bool Started => started;

        public void AddEndpoint(HttpEndpointConfig endpoint)
        {
            lock (configLock)
            {
                //if(started) throw new Exception("Endpoints can only be configured before the server is started.");
                endpoints.Add(endpoint);
                if (started)
                {
                    Stop().Wait();
                    Start();
                }
            }
        }

        public void AddRequestHandler(IWebRequestHandler handler)
        {
            lock (configLock)
            {
                var requestHandlers = this.requestHandlers;
                if (started) requestHandlers = new SortedList<string, IWebRequestHandler>(requestHandlers);
                requestHandlers.Add(handler.Route, handler);
                this.requestHandlers = requestHandlers;
                if (started)
                {
                    Stop().Wait();
                    Start();
                }
            }
        }

        public void RemoveRequestHandler(IWebRequestHandler handler)
        {
            lock (configLock)
            {
                var requestHandlers = this.requestHandlers;
                if (started) requestHandlers = new SortedList<string, IWebRequestHandler>(requestHandlers);
                requestHandlers.Remove(handler.Route);
                this.requestHandlers = requestHandlers;
                if (started)
                {
                    Stop().Wait();
                    Start();
                }
            }
        }

        public void ClearRequestHandlers()
        {
            lock (configLock)
            {
                var requestHandlers = this.requestHandlers;
                if (started) requestHandlers = new SortedList<string, IWebRequestHandler>(requestHandlers);
                else requestHandlers.Clear();
                this.requestHandlers = requestHandlers;
                if (started)
                {
                    Stop().Wait();
                    Start();
                }
            }
        }

        public void ClearEndpoints()
        {
            lock (configLock)
            {
                if (started) throw new Exception("Endpoints can only be configured before the server is started.");
                endpoints.Clear();
                if (started)
                {
                    Stop().Wait();
                    Start();
                }
            }
        }

        public void ExecuteHandlers(IApplicationBuilder app)
        {
            var requestHandlers = this.requestHandlers;
            foreach (var handler in requestHandlers)
            {
                app.Map(handler.Value.Route, app2 => app2.Run(async context =>
                {
                    ContextWrapper contextWrapper = new ContextWrapper(context);
                    await handler.Value.HandleRequestAsync(contextWrapper, contextWrapper);
                }));
            }
        }

        private class ContextWrapper : IWebRequest, IWebResponse
        {
            private HttpContext context;

            public ContextWrapper(HttpContext context)
            {
                this.context = context;
            }

            string IWebRequest.BasePath => context.Request.PathBase;

            string IWebRequest.FullPath => context.Request.PathBase + context.Request.Path;

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

            string IWebRequest.RelativePath => context.Request.Path;

            HttpStatusCode IWebResponse.StatusCode { get => (HttpStatusCode)context.Response.StatusCode; set => context.Response.StatusCode = (int)value; }

            string IWebRequest.HostAddress
            {
                get
                {
                    return (context.Request.IsHttps ? "https://" : "http://") + context.Request.Host.Value;
                }
            }

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

            Task IWebResponse.WriteAsync(string reply)
            {
                return context.Response.WriteAsync(reply);
            }
        }

        public void Start()
        {
            Task.Run(() =>
            {
                started = true;

                this.webserver = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Limits.MaxRequestBodySize = null;
                    options.AllowSynchronousIO = false;
                    foreach (var endpoint in endpoints)
                    {
                        if (endpoint.address == null) continue;

                        if (endpoint.certificateName != null)
                        {
                            if (Storage.GetReader("certificate").TryRead(endpoint.certificateName, out X509Certificate2 certificate))
                            {
                                options.Listen(endpoint.address, endpoint.port, listenOptions =>
                                {
                                    listenOptions.UseHttps(certificate);
                                });
                            }
                            else
                            {
                                Log.ERROR(this, $"Certificate {endpoint.certificateName} could not be retreived! Endpoint was not established.");
                            }
                        }
                        else
                        {
                            options.Listen(endpoint.address, endpoint.port);
                        }
                    }
                })
                /*.ConfigureServices(services =>
                {
                    foreach (var extend in serviceExtensions)
                    {
                        extend(services);
                    }
                })*/
                .Configure(app =>
                {
                    app.Map("", ExecuteHandlers);
                })
                .Build();

                this.webserver.Run();
                this.started = false;
            });
        }

        public async Task Stop()
        {
            if (webserver != null)
            {
                await webserver.StopAsync();
            }
        }
    }
}