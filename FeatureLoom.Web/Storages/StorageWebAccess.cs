using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace FeatureLoom.Storages
{
    public class StorageWebAccess<T> : IWebRequestHandler
    {
        public class Config : Configuration
        {
            public bool allowChange = false;
            public bool allowRead = true;
            public bool allowReadUrls = true;
            public string category = "wwwroot";
            public string defaultContentType = "application/octet-stream";
            public Dictionary<string, string> fileEndingToContentType = new Dictionary<string, string>()
            {
                {"html", "text/html"},
                {"htm", "text/html"},
                {"json", "application/json"},
                {"css", "text/css"},
                {"js", "text/js"},
                {"txt", "text/plain"},
                {"xml", "application/xml"},
                {"pdf", "application/pdf"},
                {"jpg", "image/jpeg"},
                {"jpeg", "image/jpeg"},
                {"png", "image/png"},
                {"gif", "image/gif"},
                {"mp3", "audio/mpeg"},
                {"wav", "audio/wav"},
                {"mp4", "video/mp4"},
                {"mpeg", "video/mpeg"}
            };
        }
        private Config config;

        private IStorageReader reader;
        private IStorageWriter writer;
        private string route;

        public StorageWebAccess(string route, Config config = null)
        {
            this.config = config ?? new Config(){};            
            this.config.TryUpdateFromStorage(false);

            route = route.TrimEnd("/");
            if (!route.StartsWith("/")) route = "/" + route;            
            this.route = route;

            reader = Storage.GetReader(this.config.category);
            writer = Storage.GetWriter(this.config.category);
        }

        public string Route => route;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            try
            {
                if (request.IsGet && config.allowReadUrls && request.RelativePath=="")
                {
                    if (request.TryGetQueryItem("uris", out string pattern))
                    {
                        return await ReadUrlsAsync(pattern);
                    }
                    else
                    {
                        return await ReadUrlsAsync(null);
                    }
                }
                else if (request.IsGet && config.allowRead) return await ReadAsync(request, response);
                else if (request.IsPut && config.allowChange) return await WriteAsync(request, response);
                else if (request.IsDelete && config.allowChange) return await DeleteAsync(request, response);
                else
                {                    
                    return HandlerResult.Handled_MethodNotAllowed();
                }
            }
            catch (Exception e)
            {
                Log.WARNING(this.GetHandle(), $"Failed storage web access ({request.Method}, {request.RelativePath})", e.ToString());                
                return HandlerResult.Handled_InternalServerError();
            }
        }

        private async Task<HandlerResult> ReadUrlsAsync(string pattern)
        {
            if (pattern == "") pattern = null;
            if ((await reader.TryListUrisAsync(pattern)).TryOut(out string[] uris))
            {
                /*for(int i = 0; i < uris.Length; i++)
                {
                    uris[i] = request.HostAddress + request.BasePath + "/" + uris[i].Replace("\\", "/");
                }*/
                //JSON.net does not provide async write to stream, so serialization has to be done before

                return HandlerResult.Handled_OK(uris);
            }
            else
            {
                return HandlerResult.Handled_OK(Array.Empty<string>());
            }
        }

        private async Task<HandlerResult> DeleteAsync(IWebRequest request, IWebResponse response)
        {
            if (await writer.TryDeleteAsync(request.RelativePath.TrimChar('/')))
            {                
                return HandlerResult.Handled_OK();
            }
            else
            {                
                return HandlerResult.Handled_InternalServerError();
            }
        }

        private async Task<HandlerResult> WriteAsync(IWebRequest request, IWebResponse response)
        {
            if (typeof(T).IsAssignableFrom(typeof(string)) ||
               typeof(T).IsAssignableFrom(typeof(byte[])))
            {
                if (await writer.TryWriteAsync(request.RelativePath.Replace("%20", " ").TrimChar('/'), request.Stream))
                {                    
                    return HandlerResult.Handled_OK();
                }
                else
                {                    
                    return HandlerResult.Handled_InternalServerError();
                }
            }
            else
            {
                T obj = request.Stream.FromJson<T>();
                if (await writer.TryWriteAsync(request.RelativePath.Replace("%20", " ").TrimChar('/'), obj))
                {
                    return HandlerResult.Handled_OK();
                }
                else
                {
                    return HandlerResult.Handled_InternalServerError();
                }
            }
        }

        private async Task<HandlerResult> ReadAsync(IWebRequest request, IWebResponse response)
        {
            string uri = request.RelativePath.Replace("%20", " ").TrimChar('/');

            if (typeof(T).IsAssignableFrom(typeof(string)) ||
                typeof(T).IsAssignableFrom(typeof(byte[])))
            {
                int endingStart = uri.LastIndexOf('.') + 1;
                string ending = "";
                if (endingStart > 0 && endingStart < uri.Length) ending = uri.Substring(endingStart);

                if (config.fileEndingToContentType != null &&
                    config.fileEndingToContentType.TryGetValue(ending, out string contentType))
                {
                    response.ContentType = contentType;
                }
                else if (config.defaultContentType != null)
                {
                    response.ContentType = config.defaultContentType;
                }

                response.StatusCode = HttpStatusCode.OK;

                if (await reader.TryReadAsync(uri, s => s.CopyToAsync(response.Stream)))
                {
                    return HandlerResult.Handled_OK();
                }
                else
                {
                    return HandlerResult.Handled_NotFound();
                }
            }
            else
            {
                response.ContentType = config.defaultContentType ?? "application/json";

                if ((await reader.TryReadAsync<T>(uri)).TryOut(out T obj))
                {
                    return HandlerResult.Handled_OK(obj);
                }
                else
                {                    
                    return HandlerResult.Handled_NotFound();
                }
            }
        }
    }
}