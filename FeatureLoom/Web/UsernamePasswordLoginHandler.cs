using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Security;
using FeatureLoom.Serialization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class UsernamePasswordLoginHandler : IWebRequestHandler
    {
        public string route = "/Login";
        public TimeSpan normalizedProcessingTime = 100.Milliseconds();

        public string Route => route;

        ICredentialHandler<UsernamePassword> credentialHandler = new UserNamePasswordPBKDF2Handler();

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (request.IsPost)
            {
                TimeFrame processingTimeFrame = new TimeFrame(normalizedProcessingTime);
                try
                {
                    string data = await request.ReadAsync();
                    var usernamePassword = data.FromJson<UsernamePassword>();

                    if ((await Identity.TryLoadIdentityAsync(usernamePassword.username)).Out(out Identity identity))
                    {
                        if (identity.TryGetCredential(credentialHandler.CredentialType, out var storedCredential) && 
                            credentialHandler.VerifyCredential(usernamePassword, storedCredential))
                        {
                            Session session = new Session(identity);
                            if (await session.TryStoreAsync())
                            {
                                response.AddCookie("SessionId", session.SessionId, new Microsoft.AspNetCore.Http.CookieOptions() { MaxAge = session.Timeout});
                                response.StatusCode = HttpStatusCode.OK;
                                Log.INFO(this.GetHandle(), $"Login successful by user [{usernamePassword.username}]");
                                response.StatusCode = HttpStatusCode.OK;
                                await response.WriteAsync(session.SessionId);
                            }
                            else
                            {
                                Log.ERROR(this.GetHandle(), "Failed to store session!");
                                response.StatusCode = HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            Log.INFO(this.GetHandle(), "Login failed, due to wrong credentials!");
                            response.StatusCode = HttpStatusCode.Unauthorized;
                        }
                    }
                    else
                    {
                        Log.INFO(this.GetHandle(), "Login failed, due to unknown user!");
                        response.StatusCode = HttpStatusCode.Unauthorized;
                    }

                }
                catch(Exception e)
                {
                    Log.WARNING(this.GetHandle(), "Failed to process login data", e.ToString());
                    response.StatusCode = HttpStatusCode.BadRequest;
                }

                await processingTimeFrame.WaitForEndAsync();
                return true;
            }
            else return false;
        }
    }
}
