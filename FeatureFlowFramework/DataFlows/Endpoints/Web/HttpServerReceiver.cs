using FeatureFlowFramework.Logging;
using FeatureFlowFramework.Web;
using System;
using System.Net;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.Web
{
    public class HttpServerReceiver : IDataFlowSource, IWebRequestHandler
    {
        private readonly int bufferSize;
        private readonly string route;
        private IWebMessageTranslator translator;
        private readonly IWebServer webServer;

        private DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();

        public HttpServerReceiver(string route, IWebMessageTranslator translator, int bufferSize = 1024 * 128, IWebServer webServer = null)
        {
            this.route = route;
            this.translator = translator;
            this.webServer = webServer ?? SharedWebServer.WebServer;
            this.bufferSize = bufferSize;

            this.webServer.AddRequestHandler(this);
        }

        public int CountConnectedSinks => ((IDataFlowSource)sendingHelper).CountConnectedSinks;

        public string Route => route;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sendingHelper).ConnectTo(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sendingHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sendingHelper).GetConnectedSinks();
        }

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if(!request.IsPost)
            {
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                await response.WriteAsync("Use 'POST' to send messages!");
                return false;
            }

            try
            {
                string bodyString = await request.ReadAsync();
                if(translator.TryTranslate(bodyString, out object message))
                {
                    await sendingHelper.ForwardAsync(message);
                }
                else
                {
                    Log.WARNING(this, $"Received message could not be translated. Route:{route}");
                    response.StatusCode = HttpStatusCode.InternalServerError;
                }
            }
            catch(Exception e)
            {
                Log.ERROR(this, $"Failed while reading, translating or sending a message from a post command. Route:{route}", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            return true;
        }
    }
}