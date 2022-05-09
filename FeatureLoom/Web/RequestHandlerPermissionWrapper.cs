using FeatureLoom.Security;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{    
    public class RequestHandlerPermissionWrapper : IWebRequestHandler
    {
        IWebRequestHandler handler;
        string requiredPermission;
        bool returnForbidden;

        public RequestHandlerPermissionWrapper(IWebRequestHandler handler, string requiredPermission, bool returnForbidden)
        {
            this.handler = handler;
            this.requiredPermission = requiredPermission;
            this.returnForbidden = returnForbidden;
        }

        public string Route => handler.Route;

        public Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (Session.Current?.Identity?.HasPermission(requiredPermission) ?? false)
            {
                return handler.HandleRequestAsync(request, response);
            }
            else if (returnForbidden)
            {                
                return Task.FromResult(HandlerResult.Handled_Forbidden());
            }
            else
            {
                return Task.FromResult(HandlerResult.NotHandled());
            }
        }
    }
}
