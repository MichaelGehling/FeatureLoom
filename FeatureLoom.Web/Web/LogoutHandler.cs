using FeatureLoom.Security;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class LogoutHandler : IWebRequestHandler
    {
        public string cookieName = "SessionId";
        public string route = "/Logout";

        public string Route => route;

        string[] supportedMethods = { "POST" };

        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => true;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (request.RelativePath != "") return HandlerResult.NotHandled();            
            if (!request.IsPost) return HandlerResult.Handled_MethodNotAllowed();

            Session session = Session.Current;
            if (session != null)
            {
                Session.Current = null;
                await session.TryDeleteFromStorageAsync();
                response.DeleteCookie(cookieName);
            }

            return HandlerResult.Handled_OK();

        }
    }
}
