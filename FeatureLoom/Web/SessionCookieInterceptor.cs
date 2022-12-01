using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Security;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    /// <summary>
    /// Checks for a session cookie and loads and sets up the session object according to the session id stored in the cookie.
    /// The session object can then be accessed via Session.Current within this "logical thread".
    /// By default an exceeded session object will be removed together with the cookie, but that can be turned off to be handled seperately.
    /// </summary>
    public class SessionCookieInterceptor : IWebRequestInterceptor
    {
        public string cookieName = "SessionId";
        public bool removeExceededSessionAndCookie = true;
        public string anonymousIdentity = "Anonymous";
        public bool supportSessionIdInQueryString = true;

        public async Task<HandlerResult> InterceptRequestAsync(IWebRequest request, IWebResponse response)
        {            
            if (request.TryGetCookie(cookieName, out string sessionId) ||
                (supportSessionIdInQueryString && request.TryGetQueryItem("SessionId", out sessionId)))
            {
                if ((await Session.TryLoadSessionAsync(sessionId)).TryOut(out Session session))
                {
                    if (session.SessionId == sessionId && !session.LifeTime.Elapsed())
                    {
                        if (Identity.Exists(session.IdentityId))
                        {
                            if (session.Refresh())
                            {
                                response.AddCookie(cookieName, session.SessionId, new Microsoft.AspNetCore.Http.CookieOptions() { MaxAge = session.Timeout });
                            }
                            Session.Current = session;
                            return HandlerResult.NotHandled();
                        }
                        else
                        {
                            Log.WARNING(this.GetHandle(), $"Identity {session.IdentityId} of session does not exist! Session will be invalidated.");
                            await session.TryDeleteFromStorageAsync();
                            response.DeleteCookie(cookieName);
                            return HandlerResult.NotHandled();
                        }
                    }
                    else if (removeExceededSessionAndCookie)
                    {                        
                        _ = session.TryDeleteFromStorageAsync();
                        response.DeleteCookie(cookieName);
                    }
                }
            }

            if (!anonymousIdentity.EmptyOrNull())
            {
                if (!(await Identity.TryLoadIdentityAsync(anonymousIdentity)).TryOut(out Identity identity))
                {
                    identity = new Identity(anonymousIdentity, null);
                    _ = identity.TryStoreAsync();
                }
                Session session = new Session(identity);
                Session.Current = session;
                _ = session.TryStoreAsync();
            }

            return HandlerResult.NotHandled();
        }
    }
}
