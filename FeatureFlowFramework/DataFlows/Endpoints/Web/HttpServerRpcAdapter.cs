using FeatureFlowFramework.DataFlows.RPC;
using FeatureFlowFramework.Logging;
using FeatureFlowFramework.Web;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.Web
{
    public class HttpServerRpcAdapter : IWebRequestHandler, IRequester
    {
        private StringRpcCaller rpcCaller;
        private readonly string route;
        private readonly IWebServer webServer;
        private readonly TimeSpan rpcTimeout;

        public HttpServerRpcAdapter(string route, TimeSpan rpcTimeout, IWebServer webServer = null)
        {
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
            if(!request.IsPost)
            {
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                await response.WriteAsync("Use 'POST' to send request messages!");
                return false;
            }

            try
            {
                int timeout = Timeout.Infinite;
                if(request.TryGetQueryItem("timeout", out string timeoutStr)) int.TryParse(timeoutStr, out timeout);

                string rpcRequest = await request.ReadAsync();
                rpcRequest = rpcRequest.Trim();
                string rpcResponse = await rpcCaller.CallAsync(rpcRequest);
                await response.WriteAsync(rpcResponse);
            }
            catch(TaskCanceledException cancelException)
            {
                response.StatusCode = HttpStatusCode.RequestTimeout;
                Log.WARNING(this, "Web RPC request timed out", cancelException.ToString());
            }

            catch(Exception e)
            {
                Log.ERROR(this, $"Failed while building response! Route:{route}", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            return true;
        }

        public void ConnectToAndBack(IReplier replier)
        {
            ((IRequester)rpcCaller).ConnectToAndBack(replier);
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IRequester)rpcCaller).ConnectTo(sink);
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
    }
}