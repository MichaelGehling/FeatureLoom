using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class RpcCallee : IDataFlowSink, IDataFlowSource, IReplier
    {
        private DataFlowSourceHelper sourceHelper = new DataFlowSourceHelper();
        private List<IRpcRequestHandler> requestHandlers = new List<IRpcRequestHandler>();

        private void AddRpcRequestHandler(IRpcRequestHandler handler)
        {
            lock(requestHandlers)
            {
                var newRequestHandlers = new List<IRpcRequestHandler>(requestHandlers);
                handler.SetTarget(this.sourceHelper);
                newRequestHandlers.Add(handler);
                requestHandlers = newRequestHandlers;
            }
        }

        public virtual void Post<M>(in M message)
        {
            if(message is IRpcRequest || message is string)
            {
                bool handled = false;
                for(int i = 0; i < requestHandlers.Count; i++)
                {
                    if(requestHandlers[i].Handle(message))
                    {
                        handled = true;
                        break;
                    }
                }

                if(!handled && message is IRpcRequest request) sourceHelper.Forward(new RpcErrorResponse(request.RequestId, $"No matching method registered for {request.Method}"));
            }
        }

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }

        public int CountConnectedSinks => ((IDataFlowSource)sourceHelper).CountConnectedSinks;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sourceHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sourceHelper).GetConnectedSinks();
        }

        private interface IRpcRequestHandler
        {
            bool Handle<M>(in M message);

            void SetTarget(DataFlowSourceHelper target);
        }

        public void RegisterMethod<R>(string methodName, Func<R> method)
        {
            RegisterMethod<bool, R>(methodName, p => method());
        }

        public void RegisterMethod<P1, R>(string methodName, Func<P1, R> method)
        {
            AddRpcRequestHandler(new RpcRequestHandler<P1, R>(methodName, method));
        }

        public void RegisterMethod<P1, P2, R>(string methodName, Func<P1, P2, R> method)
        {
            RegisterMethod<(P1, P2), R>(methodName, p => method(p.Item1, p.Item2));
        }

        public void RegisterMethod<P1, P2, P3, R>(string methodName, Func<P1, P2, P3, R> method)
        {
            RegisterMethod<(P1, P2, P3), R>(methodName, p => method(p.Item1, p.Item2, p.Item3));
        }

        public void RegisterMethod<P1, P2, P3, P4, R>(string methodName, Func<P1, P2, P3, P4, R> method)
        {
            RegisterMethod<(P1, P2, P3, P4), R>(methodName, p => method(p.Item1, p.Item2, p.Item3, p.Item4));
        }

        public void RegisterMethod<P1, P2, P3, P4, P5, R>(string methodName, Func<P1, P2, P3, P4, P5, R> method)
        {
            RegisterMethod<(P1, P2, P3, P4, P5), R>(methodName, p => method(p.Item1, p.Item2, p.Item3, p.Item4, p.Item5));
        }

        public void RegisterMethod(string methodName, Action method)
        {
            RegisterMethod<bool, bool>(methodName, p => { method(); return true; });
        }

        public void RegisterMethod<P1>(string methodName, Action<P1> method)
        {
            RegisterMethod<P1, bool>(methodName, p => { method(p); return true; });
        }

        public void RegisterMethod<P1, P2>(string methodName, Action<P1, P2> method)
        {
            RegisterMethod<(P1, P2), bool>(methodName, p => { method(p.Item1, p.Item2); return true; });
        }

        public void RegisterMethod<P1, P2, P3>(string methodName, Action<P1, P2, P3> method)
        {
            RegisterMethod<(P1, P2, P3), bool>(methodName, p => { method(p.Item1, p.Item2, p.Item3); return true; });
        }

        public void RegisterMethod<P1, P2, P3, P4>(string methodName, Action<P1, P2, P3, P4> method)
        {
            RegisterMethod<(P1, P2, P3, P4), bool>(methodName, p => { method(p.Item1, p.Item2, p.Item3, p.Item4); return true; });
        }

        public void RegisterMethod<P1, P2, P3, P4, P5>(string methodName, Action<P1, P2, P3, P4, P5> method)
        {
            RegisterMethod<(P1, P2, P3, P4, P5), bool>(methodName, p => { method(p.Item1, p.Item2, p.Item3, p.Item4, p.Item5); return true; });
        }
    }
}