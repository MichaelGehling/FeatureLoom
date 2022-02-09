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

        public Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (Session.Current?.Identity?.HasPermission(requiredPermission) ?? false)
            {
                return handler.HandleRequestAsync(request, response);
            }
            else if (returnForbidden)
            {
                response.StatusCode = System.Net.HttpStatusCode.Forbidden;
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }
        }
    }
}
