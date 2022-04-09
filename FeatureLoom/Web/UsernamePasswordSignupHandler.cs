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

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (request.IsPost && request.RelativePath == "")
            {
                try
                {
                    string data = await request.ReadAsync();
                    var usernamePassword = data.FromJson<UsernamePassword>();

                    if ((await Identity.TryLoadIdentityAsync(usernamePassword.username)).ReturnValue)
                    {
                        response.StatusCode = HttpStatusCode.Conflict;
                        await response.WriteAsync($"Username \"{usernamePassword.username}\" is already existing.");
                    }
                    else
                    {
                        ICredentialHandler<UsernamePassword> credentialHandler = new UserNamePasswordPBKDF2Handler();
                        var identity = new Identity(usernamePassword.username, credentialHandler.GenerateStoredCredential(usernamePassword));
                        if (defaultRole != null) identity.AddRole(defaultRole);                        

                        if (!await identity.TryStoreAsync())
                        {
                            Log.ERROR(this.GetHandle(), $"Failed storing new identity {usernamePassword.username}");
                            response.StatusCode = HttpStatusCode.InternalServerError;                            
                        }
                        else
                        {
                            Log.INFO(this.GetHandle(), $"Signup successful for user [{usernamePassword.username}]");
                            response.StatusCode = HttpStatusCode.Created;

                            Session session = new Session(identity);
                            if (await session.TryStoreAsync())
                            {
                                response.AddCookie("SessionId", session.SessionId, new Microsoft.AspNetCore.Http.CookieOptions() { MaxAge = session.Timeout });                                                                                                
                                await response.WriteAsync(session.SessionId);
                            }
                            else
                            {
                                Log.ERROR(this.GetHandle(), "Failed to store session!");                                
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    Log.WARNING(this.GetHandle(), "Failed to process signup data", e.ToString());
                    response.StatusCode = HttpStatusCode.BadRequest;
                }

                return true;
            }

            return false;
        }        
    }
}
