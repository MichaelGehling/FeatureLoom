using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using System;
using System.Net;
using System.Threading.Tasks;
using FeatureLoom.Services;

namespace FeatureLoom.Web
{
    public class HttpServerReceiver : IMessageSource, IWebRequestHandler
    {
        private readonly int bufferSize;
        private readonly string route;
        private IWebMessageTranslator translator;
        private readonly IWebServer webServer;

        private SourceValueHelper sourceHelper;

        public HttpServerReceiver(string route, IWebMessageTranslator translator, int bufferSize = 1024 * 128)
        {
            if (!route.StartsWith("/")) route = "/" + route;
            route = route.TrimEnd("/");
            this.route = route;
            this.translator = translator;
            this.bufferSize = bufferSize;
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public string Route => route;

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (!request.IsPost)
            {
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                await response.WriteAsync("Use 'POST' to send messages!");
                return false;
            }

            try
            {
                string bodyString = await request.ReadAsync();
                if (translator.TryTranslate(bodyString, out object message))
                {
                    await sourceHelper.ForwardAsync(message);
                }
                else
                {
                    Log.WARNING(this.GetHandle(), $"Received message could not be translated. Route:{route}");
                    response.StatusCode = HttpStatusCode.InternalServerError;
                }
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Failed while reading, translating or sending a message from a post command. Route:{route}", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            return true;
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }
}