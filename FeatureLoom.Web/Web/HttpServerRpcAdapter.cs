using FeatureLoom.MessageFlow;
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
        private readonly TimeSpan rpcTimeout;

        public HttpServerRpcAdapter(string route, TimeSpan rpcTimeout)
        {
            if (!route.StartsWith("/")) route = "/" + route;
            route = route.TrimEnd("/");
            this.rpcTimeout = rpcTimeout;
            rpcCaller = new StringRpcCaller(rpcTimeout);
            this.route = route;
        }

        public string Route => route;

        public int CountConnectedSinks => ((IRequester)rpcCaller).CountConnectedSinks;

        string[] supportedMethods = { "POST" };

        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => true;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (!request.IsPost) return HandlerResult.Handled_MethodNotAllowed();            

            try
            {
                int timeout = Timeout.Infinite;
                if (request.TryGetQueryItem("timeout", out string timeoutStr)) int.TryParse(timeoutStr, out timeout);

                string rpcRequest = await request.ReadAsync();
                rpcRequest = rpcRequest.Trim();
                string rpcResponse = await rpcCaller.CallAsync(rpcRequest);                

                return HandlerResult.Handled_OK(rpcResponse);
            }
            catch (TaskCanceledException cancelException)
            {
                OptLog.WARNING()?.Build("Web RPC request timed out", cancelException.ToString());
                return HandlerResult.Handled_RequestTimeout();
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed while building response! Route:{route}", e.ToString());
                return HandlerResult.Handled_InternalServerError();

            }
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink)
        {
            return ((IRequester)rpcCaller).ConnectTo(sink);
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            ((IRequester)rpcCaller).DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            ((IRequester)rpcCaller).DisconnectAll();
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return ((IRequester)rpcCaller).GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            ((IRequester)rpcCaller).Post(in message);
        }

        public void Post<M>(M message)
        {
            ((IRequester)rpcCaller).Post(message);
        }

        public Task PostAsync<M>(M message)
        {
            return ((IRequester)rpcCaller).PostAsync(message);
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            ((IMessageSource)rpcCaller).ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return ((IMessageSource)rpcCaller).ConnectTo(sink, weakReference);
        }

        public void ConnectToAndBack(IReplier replier, bool weakReference = false)
        {
            ((IRequester)rpcCaller).ConnectToAndBack(replier, weakReference);
        }
    }
}