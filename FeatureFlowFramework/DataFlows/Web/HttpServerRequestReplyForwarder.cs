using FeatureFlowFramework.DataFlows.RequestReply;
using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using FeatureFlowFramework.Web;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.Web
{
    public class HttpServerRequestReplyForwarder : IRequester, IWebRequestHandler
    {
        private Requester<object, object> requester = new Requester<object, object>();
        private readonly string route;
        private IWebMessageTranslator translator;
        private readonly IWebServer webServer;

        public HttpServerRequestReplyForwarder(string route, IWebMessageTranslator translator, IWebServer webServer = null)
        {
            this.route = route;
            this.translator = translator;
            this.webServer = webServer ?? SharedWebServer.WebServer;
            this.webServer.AddRequestHandler(this);
        }

        public int CountConnectedSinks => ((IRequester)requester).CountConnectedSinks;

        public string Route => route;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IRequester)requester).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IRequester)requester).ConnectTo(sink);
        }

        public void ConnectToAndBack(IReplier replier)
        {
            ((IRequester)requester).ConnectToAndBack(replier);
        }

        public void DisconnectAll()
        {
            ((IRequester)requester).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IRequester)requester).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IRequester)requester).GetConnectedSinks();
        }

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (!request.IsPost)
            {
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                await response.WriteAsync("Use 'POST' to send request messages!");
                return false;
            }

            try
            {
                int timeout = Timeout.Infinite;
                if (request.TryGetQueryItem("timeout", out string timeoutStr)) int.TryParse(timeoutStr, out timeout);

                string bodyString = await request.ReadAsync();
                if (translator.TryTranslate(bodyString, out object message))
                {
                    if ((await requester.TryRequestAndReceiveAsync(message, timeout.Milliseconds())).Out(out object reply))
                    {
                        if (translator.TryTranslate(reply, out string json))
                        {
                            await response.WriteAsync(json);
                        }
                        else
                        {
                            response.StatusCode = HttpStatusCode.InternalServerError;
                            await response.WriteAsync("Failed to translate response Message!");
                        }
                    }
                    else
                    {
                        response.StatusCode = HttpStatusCode.RequestTimeout;
                    }
                }
                else
                {
                    Log.WARNING(this, $"Received message could not be translated. Route:{route}");
                    response.StatusCode = HttpStatusCode.BadRequest;
                }
            }
            catch (Exception e)
            {
                Log.ERROR(this, $"Failed while building response! Route:{route}", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            return true;
        }

        public void Post<M>(in M message)
        {
            ((IRequester)requester).Post(message);
        }

        public Task PostAsync<M>(M message)
        {
            return ((IRequester)requester).PostAsync(message);
        }
    }
}