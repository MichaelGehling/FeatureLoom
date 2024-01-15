using FeatureLoom.Extensions;
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
        public string route = "/Login/UsernamePassword";
        public TimeSpan normalizedProcessingTime = 100.Milliseconds();

        public string Route => route;

        ICredentialHandler<UsernamePassword> credentialHandler = new UserNamePasswordPBKDF2Handler();

        string[] supportedMethods = { "POST" };

        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => true;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (request.RelativePath != "") return HandlerResult.NotHandled_NotFound();
            if (!request.IsPost) return HandlerResult.NotHandled_MethodNotAllowed();            
            
            TimeFrame processingTimeFrame = new TimeFrame(normalizedProcessingTime);
            try
            {
                string data = await request.ReadAsync();
                var usernamePassword = data.FromJson<UsernamePassword>();

                if ((await Identity.TryGetIdentityAsync(usernamePassword.username)).TryOut(out Identity identity))
                {
                    if (identity.TryGetCredential(credentialHandler.CredentialType, out var storedCredential) && 
                        credentialHandler.VerifyCredential(usernamePassword, storedCredential))
                    {
                        Session session = new Session(identity);
                        if (await session.TryStoreAsync())
                        {
                            response.AddCookie("SessionId", session.SessionId, new Microsoft.AspNetCore.Http.CookieOptions() { MaxAge = session.Timeout});
                            Log.INFO(this.GetHandle(), $"Login successful by user [{usernamePassword.username}]");

                            await processingTimeFrame.WaitForEndAsync();
                            return HandlerResult.Handled_OK(session.SessionId);
                        }
                        else
                        {
                            Log.ERROR(this.GetHandle(), "Failed to store session!");

                            await processingTimeFrame.WaitForEndAsync();
                            return HandlerResult.Handled_InternalServerError();
                        }
                    }
                    else
                    {
                        Log.INFO(this.GetHandle(), "Login failed, due to wrong credentials!");

                        await processingTimeFrame.WaitForEndAsync();
                        return HandlerResult.Handled_Unauthorized();
                    }
                }
                else
                {
                    Log.INFO(this.GetHandle(), "Login failed, due to unknown user [{usernamePassword.username}]!");

                    await processingTimeFrame.WaitForEndAsync();
                    return HandlerResult.Handled_Unauthorized();
                }

            }
            catch(Exception e)
            {
                Log.WARNING(this.GetHandle(), "Failed to process login data", e.ToString());

                await processingTimeFrame.WaitForEndAsync();
                return HandlerResult.Handled_BadRequest();
            }
            
        }
    }
}
