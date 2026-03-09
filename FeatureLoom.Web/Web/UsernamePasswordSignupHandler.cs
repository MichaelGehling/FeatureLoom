using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Security;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class UsernamePasswordSignupHandler : IWebRequestHandler
    {
        public string route = "/Signup/UsernamePassword";
        public IdentityRole defaultRole = null;
        public bool createSessionAfterSignup = true;
        public string Route => route;

        string[] supportedMethods = { "POST" };

        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => true;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (request.RelativePath != "") return HandlerResult.NotHandled();

            if (!request.IsPost) return HandlerResult.NotHandled_MethodNotAllowed();

            
            try
            {
                string data = await request.ReadAsync();                
                if (!JsonHelper.DefaultDeserializer.TryDeserialize(data, out UsernamePassword usernamePassword))
                {
                    OptLog.WARNING()?.Build("Failed to process signup data");
                    return HandlerResult.Handled_BadRequest();
                }

                if ((await Identity.TryGetIdentityAsync(usernamePassword.username)).Item1)
                {                    
                    return HandlerResult.Handled_Conflict($"Username \"{usernamePassword.username}\" already exists.");
                }
                else
                {
                    ICredentialHandler<UsernamePassword> credentialHandler = new UserNamePasswordPBKDF2Handler();
                    var identity = new Identity(usernamePassword.username, credentialHandler.GenerateStoredCredential(usernamePassword));
                    if (defaultRole != null) identity.AddRole(defaultRole, false);                        

                    if (!await TryHelper.TryAsync(async ()=> await identity.StoreAsync()))
                    {
                        OptLog.ERROR()?.Build($"Failed storing new identity {usernamePassword.username}");
                        return HandlerResult.Handled_InternalServerError();
                    }

                    OptLog.INFO()?.Build($"Signup successful for user [{usernamePassword.username}]");

                    if (createSessionAfterSignup)
                    {
                        OptLog.INFO()?.Build($"Creating session for new identity [{usernamePassword.username}]");

                        Session session = new Session(identity);
                        if (await session.TryStoreAsync())
                        {
                            response.AddCookie("SessionId", session.SessionId, new Microsoft.AspNetCore.Http.CookieOptions() { MaxAge = session.Timeout });
                        }
                        else
                        {
                            OptLog.ERROR()?.Build("Failed to store session!");
                        }
                    }

                    return HandlerResult.Handled_Created();
                }
            }
            catch(Exception e)
            {
                OptLog.WARNING()?.Build("Failed to process signup data", e);                
                return HandlerResult.Handled_BadRequest();
            }

        }        
    }
}
