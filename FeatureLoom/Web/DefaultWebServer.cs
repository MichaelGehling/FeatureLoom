using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
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
    public partial class DefaultWebServer : IWebServer
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
        static readonly IComparer<IWebRequestHandler> routeComparer = new GenericComparer<IWebRequestHandler>((handler1, handler2) => -handler1.Route.CompareTo(handler2.Route));
        private List<IWebRequestHandler> requestHandlers = new List<IWebRequestHandler>();
        private List<IWebRequestInterceptor> requestInterceptors = new List<IWebRequestInterceptor>();
        private List<IWebExceptionHandler> exceptionHandlers = new List<IWebExceptionHandler>();
        private List<IWebResultHandler> resultHandlers = new List<IWebResultHandler>();
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

        public void AddExceptionHandler(IWebExceptionHandler exceptionHandler)
        {
            using (myLock.Lock())
            {
                var exceptionHandlers = this.exceptionHandlers;
                if (running) exceptionHandlers = new List<IWebExceptionHandler>(exceptionHandlers);
                exceptionHandlers.Add(exceptionHandler);
                this.exceptionHandlers = exceptionHandlers;
            }
        }

        public void RemoveExceptionHandler(IWebExceptionHandler exceptionHandler)
        {
            using (myLock.Lock())
            {
                var exceptionHandlers = this.exceptionHandlers;
                if (running) exceptionHandlers = new List<IWebExceptionHandler>(exceptionHandlers);
                exceptionHandlers.Remove(exceptionHandler);
                this.exceptionHandlers = exceptionHandlers;
            }
        }

        public void ClearExceptionHandlers()
        {
            using (myLock.Lock())
            {
                var exceptionHandlers = this.exceptionHandlers;
                if (running) exceptionHandlers = new List<IWebExceptionHandler>();
                else exceptionHandlers.Clear();
                this.exceptionHandlers = exceptionHandlers;

            }
        }

        public void AddResultHandler(IWebResultHandler resultHandler)
        {
            using (myLock.Lock())
            {
                var resultHandlers = this.resultHandlers;
                if (running) resultHandlers = new List<IWebResultHandler>(resultHandlers);
                resultHandlers.Add(resultHandler);
                this.resultHandlers = resultHandlers;
            }
        }

        public void RemoveResultHandler(IWebResultHandler resultHandler)
        {
            using (myLock.Lock())
            {
                var resultHandlers = this.resultHandlers;
                if (running) resultHandlers = new List<IWebResultHandler>(resultHandlers);
                resultHandlers.Remove(resultHandler);
                this.resultHandlers = resultHandlers;
            }
        }

        public void ClearResultHandlers()
        {
            using (myLock.Lock())
            {
                var resultHandlers = this.resultHandlers;
                if (running) resultHandlers = new List<IWebResultHandler>();
                else resultHandlers.Clear();
                this.resultHandlers = resultHandlers;

            }
        }

        public void AddRequestHandler(IWebRequestHandler handler)
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new List<IWebRequestHandler>(requestHandlers);
                requestHandlers.Add(handler);
                requestHandlers.Sort(routeComparer);
                this.requestHandlers = requestHandlers;
            }
        }

        public void RemoveRequestHandler(IWebRequestHandler handler)
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new List<IWebRequestHandler>(requestHandlers);
                requestHandlers.Remove(handler);
                requestHandlers.Sort(routeComparer);
                this.requestHandlers = requestHandlers;
            }
        }

        public void ClearRequestHandlers()
        {
            using (myLock.Lock())
            {
                var requestHandlers = this.requestHandlers;
                if (running) requestHandlers = new List<IWebRequestHandler>();
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
                // Shortcut to deliver icon.
                // If icon needs to be handled dynamically in a handler, the shortcut can be skipped by setting the favicon to null.
                if (favicon != null && request.IsGet && request.Path == "/favicon.ico")
                {
                    response.StatusCode = HttpStatusCode.OK;
                    await response.Stream.WriteAsync(this.favicon, 0, favicon.Length);                    
                    return; // icon was delivered, so finish request handling
                }

                var currentInterceptors = this.requestInterceptors; // take current reference to avoid change while execution.
                foreach (var interceptor in currentInterceptors)
                {
                    var result = await interceptor.InterceptRequestAsync(request, response);
                    if (await ProcessHandlerResult(request, response, result))
                    {
                        await ReactOnResult(request, response, result);
                        return;
                    }
                }

                var currentRequestHandlers = this.requestHandlers; // take current reference to avoid change while execution.                
                foreach (var handler in currentRequestHandlers)
                {
                    if (request.Path.StartsWith(handler.Route))
                    {
                        contextWrapper.SetRoute(handler.Route);
                        HandlerResult result = await handler.HandleRequestAsync(request, response);
                        if (await ProcessHandlerResult(request, response, result))
                        {
                            await ReactOnResult(request, response, result);
                            return;
                        }
                    }
                }

                // Request was not handled so NotFound StatusCode is set, but it has to be ensured that the response stream was not already written.
                if (!response.ResponseSent && !response.StatusCodeSet)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await ReactOnResult(request, response, HandlerResult.NotHandled_NotFound());
                    return;
                }
            }
            catch (WebResponseException e)
            {
                if (e.LogLevel.HasValue) Log.SendLogMessage(new LogMessage(e.LogLevel.Value, e.InternalMessage));
                response.StatusCode = e.StatusCode;
                if (!e.ResponseMessage.EmptyOrNull()) await response.WriteAsync(e.ResponseMessage);                
                await ReactOnResult(request, response, new HandlerResult(true, e.ResponseMessage, e.StatusCode));
                return;
            }            
            catch (Exception e)
            {
                foreach (var exceptionHanlder in exceptionHandlers)
                {
                    var result = await exceptionHanlder.HandleException(e, request, response);
                    if (await ProcessHandlerResult(request, response, result))
                    {
                        await ReactOnResult(request, response, result);
                        return;
                    }
                }

                Log.ERROR(this.GetHandle(), "Web request failed with an unhandled exception!", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;                
                await ReactOnResult(request, response, HandlerResult.NotHandled_InternalServerError());
                return;
            }
        }

        private  async Task<bool> ProcessHandlerResult(IWebRequest request, IWebResponse response, HandlerResult result)
        {
            if (response.ResponseSent)
            {
                if (result.statusCode != response.StatusCode) Log.WARNING(this.GetHandle(), $"Response was already sent, but status code of result ({result.statusCode}) and response ({response.StatusCode}) differ!");
                if (result.data != null) Log.WARNING(this.GetHandle(), $"Response was already sent, but result contained data, that cannot be delivered!");
                return true;
            }

            if (result.statusCode.HasValue) response.StatusCode = result.statusCode.Value;
            if (result.data != null)
            {
                if (!result.statusCode.HasValue) response.StatusCode = HttpStatusCode.OK;

                if (result.data is string str) await response.WriteAsync(str);
                else if (result.data is byte[] bytes) await response.Stream.WriteAsync(bytes, 0, bytes.Length);
                else if (result.data is Stream stream) await stream.CopyToAsync(response.Stream);
                else await response.WriteAsync(Json.SerializeToJson(result.data));

                return true;
            }
            return result.requestHandled;
        }

        private async Task ReactOnResult(IWebRequest request, IWebResponse response, HandlerResult result)
        {
            foreach (var resultHandler in this.resultHandlers)
            {
                await resultHandler.HandleResult(result, request, response);
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

    }
}