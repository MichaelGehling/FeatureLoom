using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using FeatureFlowFramework.Web;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataStorage
{
    public class StorageWebAccess<T> : IWebRequestHandler
    {
        public class Config : Configuration
        {
            public bool allowChange = false;
            public bool allowRead = true;
            public bool allowReadUrls = true;
            public bool applyFilter = false;
            public List<string> filter = new List<string>();
            public string category = "wwwroot";
            public string route = "/";
        }

        private Config config;

        private IStorageReader reader;
        private IStorageWriter writer;

        public StorageWebAccess(Config config = null, IWebServer webServer = null)
        {
            this.config = config ?? new Config();
            this.config.TryUpdateFromStorageAsync(false).Wait();
            webServer = webServer ?? SharedWebServer.WebServer;
            webServer.AddRequestHandler(this);

            reader = Storage.GetReader(config.category);
            writer = Storage.GetWriter(config.category);
        }

        public string Route => config.route;

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            try
            {
                if(request.IsGet && request.TryGetQueryItem("uris", out string pattern) && config.allowReadUrls) return await ReadUrlsAsync(request, response, pattern);
                else if(request.IsGet && config.allowRead) return await ReadAsync(request, response);
                else if(request.IsPut && config.allowChange) return await WriteAsync(request, response);
                else if(request.IsDelete && config.allowChange) return await DeleteAsync(request, response);
                else
                {
                    response.StatusCode = HttpStatusCode.MethodNotAllowed;
                    return false;
                }
            }
            catch(Exception e)
            {
                Log.WARNING(this, $"Failed storage web access ({request.Method}, {request.RelativePath})", e.ToString());
                return true;
            }
        }

        private async Task<bool> ReadUrlsAsync(IWebRequest request, IWebResponse response, string pattern)
        {
            if(pattern == "") pattern = null;
            if((await reader.TryListUrisAsync(pattern)).Out(out string[] uris))
            {
                /*for(int i = 0; i < uris.Length; i++)
                {
                    uris[i] = request.HostAddress + request.BasePath + "/" + uris[i].Replace("\\", "/");
                }*/
                //JSON.net does not provide async write to stream, so serialization has to be done before
                string json = uris.ToJson();
                await response.WriteAsync(json);
                return true;
            }
            else
            {
                response.StatusCode = HttpStatusCode.NoContent;
                return true;
            }
        }

        private async Task<bool> DeleteAsync(IWebRequest request, IWebResponse response)
        {
            if(await writer.TryDeleteAsync(request.RelativePath.Trim('/')))
            {
                return true;
            }
            else
            {
                response.StatusCode = HttpStatusCode.NoContent;
                return true;
            }
        }

        private async Task<bool> WriteAsync(IWebRequest request, IWebResponse response)
        {
            if(typeof(T).IsAssignableFrom(typeof(string)) ||
               typeof(T).IsAssignableFrom(typeof(byte[])))
            {
                if(await writer.TryWriteAsync(request.RelativePath.Trim('/'), request.Stream))
                {
                    return true;
                }
                else
                {
                    response.StatusCode = HttpStatusCode.NotAcceptable;
                    return true;
                }
            }
            else
            {
                T obj = request.Stream.FromJson<T>();
                if(await writer.TryWriteAsync(request.RelativePath.Trim('/'), obj))
                {
                    return true;
                }
                else
                {
                    response.StatusCode = HttpStatusCode.NotAcceptable;
                    return true;
                }
            }
        }

        private async Task<bool> ReadAsync(IWebRequest request, IWebResponse response)
        {
            if(typeof(T).IsAssignableFrom(typeof(string)) ||
               typeof(T).IsAssignableFrom(typeof(byte[])))
            {
                if(await reader.TryReadAsync(request.RelativePath.Trim('/'), s => s.CopyToAsync(response.Stream)))
                {
                    return true;
                }
                else
                {
                    response.StatusCode = HttpStatusCode.NoContent;
                    return true;
                }
            }
            else
            {
                if((await reader.TryReadAsync<T>(request.RelativePath.Trim('/'))).Out(out T obj))
                {
                    //JSON.net does not provide async write to stream, so serialization has to be done before
                    string json = obj.ToJson();
                    await response.WriteAsync(json);
                    return true;
                }
                else
                {
                    response.StatusCode = HttpStatusCode.NoContent;
                    return true;
                }
            }
        }
    }
}