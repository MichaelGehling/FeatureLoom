using FeatureLoom.DataFlows;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.RPC;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class HttpServerRpcAdapter : IWebRequestHandler, IRequester
    {
        private StringRpcCaller rpcCaller;
        private readonly string route;
        private readonly IWebServer webServer;
        private readonly TimeSpan rpcTimeout;

        public HttpServerRpcAdapter(string route, TimeSpan rpcTimeout, IWebServer webServer = null)
        {
            if (!route.StartsWith("/")) route = "/" + route;
            route = route.TrimEnd("/");
            this.rpcTimeout = rpcTimeout;
            rpcCaller = new StringRpcCaller(rpcTimeout);
            this.route = route;
            this.webServer = webServer ?? SharedWebServer.WebServer;
            this.webServer.AddRequestHandler(this);
        }

        public string Route => route;

        public int CountConnectedSinks => ((IRequester)rpcCaller).CountConnectedSinks;

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

                string rpcRequest = await request.ReadAsync();
                rpcRequest = rpcRequest.Trim();
                string rpcResponse = await rpcCaller.CallAsync(rpcRequest);
                await response.WriteAsync(rpcResponse);
            }
            catch (TaskCanceledException cancelException)
            {
                response.StatusCode = HttpStatusCode.RequestTimeout;
                Log.WARNING(this.GetHandle(), "Web RPC request timed out", cancelException.ToString());
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Failed while building response! Route:{route}", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            return true;
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IRequester)rpcCaller).ConnectTo(sink);
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IRequester)rpcCaller).DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            ((IRequester)rpcCaller).DisconnectAll();
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IRequester)rpcCaller).GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            ((IRequester)rpcCaller).Post(message);
        }

        public Task PostAsync<M>(M message)
        {
            return ((IRequester)rpcCaller).PostAsync(message);
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)rpcCaller).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)rpcCaller).ConnectTo(sink, weakReference);
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            ((IRequester)rpcCaller).ConnectToAndBack(replier, weakReference);
        }
    }
}