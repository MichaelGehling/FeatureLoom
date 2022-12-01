using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
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
        public string Route => route;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (request.RelativePath != "") return HandlerResult.NotHandled();

            if (!request.IsPost) return HandlerResult.Handled_MethodNotAllowed();

            
            try
            {
                string data = await request.ReadAsync();
                var usernamePassword = data.FromJson<UsernamePassword>();

                if ((await Identity.TryLoadIdentityAsync(usernamePassword.username)).Item1)
                {                    
                    return HandlerResult.Handled_Conflict($"Username \"{usernamePassword.username}\" already exists.");
                }
                else
                {
                    ICredentialHandler<UsernamePassword> credentialHandler = new UserNamePasswordPBKDF2Handler();
                    var identity = new Identity(usernamePassword.username, credentialHandler.GenerateStoredCredential(usernamePassword));
                    if (defaultRole != null) identity.AddRole(defaultRole);                        

                    if (!await identity.TryStoreAsync())
                    {
                        Log.ERROR(this.GetHandle(), $"Failed storing new identity {usernamePassword.username}");
                        return HandlerResult.Handled_InternalServerError();
                    }
                    else
                    {
                        Log.INFO(this.GetHandle(), $"Signup successful for user [{usernamePassword.username}]");                        

                        Session session = new Session(identity);
                        if (await session.TryStoreAsync())
                        {
                            response.AddCookie("SessionId", session.SessionId, new Microsoft.AspNetCore.Http.CookieOptions() { MaxAge = session.Timeout });                            
                        }
                        else
                        {
                            Log.ERROR(this.GetHandle(), "Failed to store session!");
                        }

                        return HandlerResult.Handled_Created();
                    }
                }
            }
            catch(Exception e)
            {
                Log.WARNING(this.GetHandle(), "Failed to process signup data", e.ToString());                
                return HandlerResult.Handled_BadRequest();
            }

        }        
    }
}
