using FeatureLoom.Collections;
using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Time;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Serialization;

namespace FeatureLoom.Web
{
    public class HttpServerFetchProvider : IMessageSink, IWebRequestHandler
    {
        private CircularLogBuffer<string> ringBuffer;
        private readonly string route;
        public string Route => route;

        string[] supportedMethods = { "GET" };

        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => true;

        public HttpServerFetchProvider(string route, int bufferSize = 100)
        {
            if (!route.StartsWith("/")) route = "/" + route;
            route = route.TrimEnd("/");
            this.route = route;
            ringBuffer = new CircularLogBuffer<string>(bufferSize);
        }

        public void Post<M>(in M message)
        {
            string json = JsonHelper.DefaultSerializer.Serialize(message);
            ringBuffer.Add(json);
        }

        public void Post<M>(M message)
        {
            string json = JsonHelper.DefaultSerializer.Serialize(message);
            ringBuffer.Add(json);
        }

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (!request.IsGet) return HandlerResult.Handled_MethodNotAllowed();            

            try
            {
                if (!request.TryGetQueryItem("next", out long requestedStart)) requestedStart = 0;
                if (!request.TryGetQueryItem("maxWait", out int maxWait)) maxWait = 0;
                if (!request.TryGetQueryItem("maxItems", out int maxItems)) maxItems = ringBuffer.MaxSize;

                string[] messages = ringBuffer.GetAllAvailable(requestedStart, maxItems, out var firstProvided, out var lastProvided);

                if (messages.Length == 0 && maxWait > 0)
                {
                    TimeFrame timer = new TimeFrame(maxWait.Milliseconds());                  
                    while (messages.Length == 0  && await ringBuffer.WaitHandle.WaitAsync(timer.Remaining()))
                    {
                        messages = ringBuffer.GetAllAvailable(requestedStart, maxItems, out firstProvided, out lastProvided);                        
                    }
                }

                long missed = 0;
                long next = 0;
                if (messages.Length > 0)
                {
                    missed = firstProvided - requestedStart;
                    next = lastProvided + 1;
                }
                else
                {
                    missed = 0;
                    next = ringBuffer.LatestId+1;
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(
$@"{{
    ""numItems"" : {messages.Length.ToString()},
    ""missed"" : {missed.ToString()},
    ""next"" : {next.ToString()},
    ""messages"" : [
");
                for (int i = 0; i < messages.Length; i++)
                {
                    sb.Append(messages[i]);
                    if (i + 1 != messages.Length) sb.Append(",\n");
                }
                sb.Append($@"
                   ]
}}");
                
                return HandlerResult.Handled_OK(sb.ToString());
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed while building response! Route:{route}", e.ToString());                
                return HandlerResult.Handled_InternalServerError();
            }
        }
    }
}