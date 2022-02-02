﻿using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
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
        public bool running = false;
        private IWebHost webserver;

        //public List<Action<IServiceCollection>> serviceExtensions = new List<Action<IServiceCollection>>();
        public SortedList<string, IWebRequestHandler> requestHandlers = new SortedList<string, IWebRequestHandler>();

        public List<HttpEndpointConfig> endpoints = new List<HttpEndpointConfig>();

        public DefaultWebServer()
        {
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
                if (running) requestHandlers = new SortedList<string, IWebRequestHandler>(requestHandlers);
                requestHandlers.Add(handler.Route, handler);
                this.requestHandlers = requestHandlers;
            }
        }

        public void RemoveRequestHandler(IWebRequestHandler handler)
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new SortedList<string, IWebRequestHandler>(requestHandlers);
                requestHandlers.Remove(handler.Route);
                this.requestHandlers = requestHandlers;
            }
        }

        public void ClearRequestHandlers()
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new SortedList<string, IWebRequestHandler>();
                else requestHandlers.Clear();
                this.requestHandlers = requestHandlers;

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

        /*
        private void ExecuteHandlers(IApplicationBuilder app)
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
        */



        public Task Run()
        {
            return Task.Run(() =>
            {
                running = true;

                this.webserver = new WebHostBuilder()
                .UseKestrel(async options =>
                {
                    options.Limits.MaxRequestBodySize = null;
                    options.AllowSynchronousIO = false;
                    foreach (var endpoint in endpoints)
                    {
                        if (endpoint.address == null) continue;

                        if (endpoint.certificateName != null)
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
                    app.Map("", app2 => app2.Run(async context =>
                    {
                        ContextWrapper contextWrapper = new ContextWrapper(context);
                        IWebRequest request = contextWrapper;
                        IWebResponse response = contextWrapper;

                        string path = context.Request.Path;                        
                        bool possibleHandlerReached = false;
                        bool handled = false;
                        var currentRequestHandlers = this.requestHandlers; // take current reference to avoid change while execution.
                        foreach (var handler in currentRequestHandlers)
                        {
                            if (path.StartsWith(handler.Key))
                            {
                                contextWrapper.SetRoute(handler.Key);
                                handled = await handler.Value.HandleRequestAsync(contextWrapper, contextWrapper);
                                if (handled) break;
                                possibleHandlerReached = true;
                            }
                            else if (possibleHandlerReached) break;
                            
                        }

                        if (!handled)
                        {
                            if (path == "/favicon.ico")
                            {
                                response.StatusCode = HttpStatusCode.OK;
                                await response.Stream.WriteAsync(Resources.favicon, 0, Resources.favicon.Length);                                
                            }
                            else response.StatusCode = HttpStatusCode.NotFound;
                        }
                    }));
                })
                .Build();

                this.webserver.Run();
                this.running = false;
            });
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

        private class ContextWrapper : IWebRequest, IWebResponse
        {
            private HttpContext context;
            private string route = "";

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
                return context.Response.WriteAsync(reply);
            }
        }        
    }
}