using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class RpcCallee
    {
        class RpcRequestHandler<P1, R> : IRpcRequestHandler
        {
            Func<P1, R> method;
            readonly string name;
            DataFlowSourceHelper target;

            public RpcRequestHandler(string name, Func<P1, R> method)
            {
                this.method = method;
                this.name = name;
            }

            public void SetTarget(DataFlowSourceHelper target)
            {
                this.target = target;
            }

            public bool Handle<M>(in M message)
            {
                if(message is RpcRequest<P1, R> myRequest && myRequest.method == this.name)
                {
                    HandleRpcRequest(myRequest);
                    return true;
                }
                else if (message is string stringMessage)
                {
                    if (stringMessage.TryParseJson(out RpcRequest<P1, R> rpcRequest) && rpcRequest.method == this.name)
                    {
                        HandleRpcRequest(rpcRequest);
                        return true;
                    }                    
                }
                return false;
            }

            private void HandleRpcRequest(RpcRequest<P1, R> myRequest)
            {
                R result = default;
                try
                {
                    result = method.Invoke(myRequest.parameterSet);
                    if (!myRequest.noResponse)
                    {
                        var response = new RpcResponse<R>(myRequest.requestId, result);
                        target.Forward(response);
                    }
                }
                catch (Exception e)
                {
                    if (myRequest.noResponse)
                    {
                        Log.ERROR(null, $"Failed executing RPC call {myRequest.method}", e.ToString());
                    }
                    else
                    {
                        string errorMessage = e.ToString();
                        var response = new RpcErrorResponse(myRequest.requestId, errorMessage);
                        target.Forward(response);
                    }
                }
            }
        }

    }

}
