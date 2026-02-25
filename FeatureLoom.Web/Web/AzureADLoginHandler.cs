using FeatureLoom.DependencyInversion;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Scheduling;
using FeatureLoom.Storages;
using FeatureLoom.Time;
using Microsoft.Identity.Client;
using System.IdentityModel.Tokens.Jwt;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using FeatureLoom.Security;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Web
{
    public class AzureADLoginHandler : IWebRequestHandler
    {
        public class Settings : Configuration
        {
            public string route = "/Login/Azure";
            public string baseUrl;
            public string tenantId;
            public string applicationClientId;
            public string applicationClientSecret;

            public bool Validate()
            {
                if (baseUrl.EmptyOrNull()) return false;
                if (tenantId.EmptyOrNull()) return false;
                if (applicationClientId.EmptyOrNull()) return false;
                if (applicationClientSecret.EmptyOrNull()) return false;
                return true;
            }
        }

        public IdentityRole defaultRole = null;
        string[] supportedMethods = new string[] {"GET"};

        public string Route => settings.route;

        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => false;

        Settings settings;
        static string[] scopes = new[] { "openid", "profile", "email", "user.read" };
        static string scopesString = string.Join(" ", scopes);
        ConcurrentDictionary<string, (DateTime timeStamp, string originalPath)> authStates = new();
        public TimeSpan authStateTimeout = 15.Minutes();
        DateTime lastCleanup = AppTime.Now;
        ISchedule cleanupSchedule;

        public AzureADLoginHandler(Settings settings) 
        {            
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.Validate()) throw new ArgumentException(nameof(settings));
            this.settings = settings;
            StartCleanupSchedule();
        }

        public void StartCleanupSchedule()
        {
            Action<DateTime> cleanUpAction = now =>
            {
                foreach (var state in authStates)
                {
                    if (now > state.Value.timeStamp + authStateTimeout) authStates.TryRemove(state.Key, out _);
                }
            };

            cleanupSchedule = cleanUpAction.ScheduleForRecurringExecution("AzureADLoginHandler_Cleanup", authStateTimeout.Multiply(0.5));
        }

        public void StopCleanupSchedule() => cleanupSchedule = null;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {            
            string callBack = settings.baseUrl + settings.route + "/callback";
            if (request.RelativePath == "")
            {
                if (!request.IsGet) return HandlerResult.NotHandled_MethodNotAllowed();

                string authorizationEndpoint = $"https://login.microsoftonline.com/{settings.tenantId}/oauth2/v2.0/authorize";
                string authState = RandomGenerator.Int64(true).ToString();
                authStates[authState] = (AppTime.Now, request.OriginalPath);
                string authUrl = $"{authorizationEndpoint}?client_id={settings.applicationClientId}&response_type=code&redirect_uri={Uri.EscapeDataString(callBack)}&scope={Uri.EscapeDataString(scopesString)}&response_mode=query&state={Uri.EscapeDataString(authState)}";

                return response.Redirect(authUrl);
            }
            else if (request.RelativePath == "/callback")
            {
                if (!request.IsGet) return HandlerResult.NotHandled_MethodNotAllowed();

                if (!request.TryGetQueryItem("code", out string authCode))
                {
                    return HandlerResult.Handled_BadRequest("Authorization code parameter missing");
                }

                if (!request.TryGetQueryItem("state", out string authState))
                {
                    return HandlerResult.Handled_BadRequest("State parameter missing");
                }

                if (!authStates.TryRemove(authState, out var state) || AppTime.Now - state.timeStamp > authStateTimeout)
                {
                    return HandlerResult.Handled_Unauthorized("Invalid state parameter");
                }

                // Initialize the MSAL app object
                var app = ConfidentialClientApplicationBuilder.Create(settings.applicationClientId)                      
                    .WithClientSecret(settings.applicationClientSecret)
                    .WithAuthority("https://login.microsoftonline.com/" + settings.tenantId)
                    .WithRedirectUri(callBack)
                    .Build();

                
                AuthenticationResult result;
                try
                {
                    result = await app.AcquireTokenByAuthorizationCode(scopes, authCode).ExecuteAsync();
                    string idToken = result.IdToken;
                    var jwtHandler = new JwtSecurityTokenHandler();
                    JwtSecurityToken jwtToken = jwtHandler.ReadJwtToken(idToken);

                    // Extract claims from the ID token
                    string userId = jwtToken.Claims.First(claim => claim.Type == "oid").Value;
                    string userEmail = jwtToken.Claims.First(claim => claim.Type == "email").Value;
                    string userName = jwtToken.Claims.First(claim => claim.Type == "name").Value;

                    string identityId = userEmail;
                    if (!(await Identity.TryGetIdentityAsync(identityId)).TryOut(out Identity identity))
                    {
                        identity = new Identity(identityId);
                        if (defaultRole != null) identity.AddRole(defaultRole, false);

                        try
                        {
                            await identity.StoreAsync();
                            OptLog.INFO()?.Build($"Signup successful for user [{identityId}]");
                        }
                        catch
                        {
                            OptLog.ERROR()?.Build($"Failed storing new identity {identityId}");
                            return HandlerResult.Handled_InternalServerError();
                        }                        
                    }

                    Session session = Session.Current;
                    if (session == null) session = new Session(identity);
                    else session.UpdateIdentity(identity);

                    if (await session.TryStoreAsync())
                    {
                        response.AddCookie("SessionId", session.SessionId, new Microsoft.AspNetCore.Http.CookieOptions() { MaxAge = session.Timeout });
                        OptLog.INFO()?.Build($"Login successful by user [{identityId}]");
                        return response.Redirect(settings.baseUrl + state.originalPath);
                    }
                    else
                    {
                        OptLog.ERROR()?.Build("Failed to store session!");
                        return HandlerResult.Handled_InternalServerError();
                    }
                }
                catch (MsalException ex)
                {
                    OptLog.ERROR()?.Build($"Error acquiring token: {ex.Message}");
                    return HandlerResult.Handled_Unauthorized("Invalid authentication code");
                }

            }
            else
            {
                return HandlerResult.NotHandled_NotFound();
            }
        }
    }
}
