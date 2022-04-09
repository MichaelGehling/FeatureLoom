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

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (request.IsPost && request.RelativePath == "")
            {
                Session session = Session.Current;
                if (session != null)
                {
                    Session.Current = null;
                    await session.TryDeleteFromStorageAsync();
                    response.DeleteCookie(cookieName);
                }

                return true;
            }
            else return false;
        }
    }
}
