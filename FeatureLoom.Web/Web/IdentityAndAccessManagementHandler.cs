using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Security;
using FeatureLoom.Storages;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class IdentityAndAccessManagementHandler : IWebRequestHandler
    {
        public string accessRight_ManageIdentities = "ManageIdentities";
        public string cookieName = "SessionId";
        public string route = "/IdentityAndAccess";
        public string Route => route;

        string[] supportedMethods = { "DELETE" };

        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => false;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            Session session = Session.Current;
            if (session?.Identity == null) HandlerResult.Handled_Forbidden();            

            var splits = request.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);            

            //TODO: Currently only supports deleting identities, should be extended to also allow creating identities

            if (splits.Length == 2 && splits[0] == "Identity")
            {
                string identityId = splits[1];

                if (request.IsDelete)
                {                    
                    if (identityId == session.IdentityId)
                    {
                        bool failed = false;
                        // Delete own identity (also possible without extra access right)
                        try 
                        {
                            await session.Identity.RemoveFromStorageAsync();
                            OptLog.INFO()?.Build($"Successfully deleted identity [{identityId}]");                            
                        }
                        catch
                        {
                            OptLog.ERROR()?.Build($"Failed deleting identity [{identityId}]");
                            failed = true;
                        }
                        
                        //Logout after deleting own identity
                        Session.Current = null;
                        await session.TryDeleteFromStorageAsync();
                        response.DeleteCookie(cookieName);

                        return failed ? HandlerResult.Handled_InternalServerError() : HandlerResult.Handled_OK();
                    }
                    else if (session.Identity.HasPermission(accessRight_ManageIdentities))
                    {
                        if (!Storage.GetReader(Identity.StorageCategory).Exists(identityId))
                        {
                            OptLog.INFO()?.Build($"Could not delete identity [{identityId}], because it does not exist.");
                            return HandlerResult.Handled_NotFound();
                        }
                        else if (await Storage.GetWriter(Identity.StorageCategory).TryDeleteAsync(identityId))
                        {
                            OptLog.INFO()?.Build($"Successfully deleted identity [{identityId}]");
                            return HandlerResult.Handled_OK();
                        }
                        else
                        {
                            OptLog.ERROR()?.Build($"Failed deleting identity [{identityId}]");
                            return HandlerResult.Handled_InternalServerError();
                        }
                        
                    }
                    else
                    {
                        OptLog.WARNING()?.Build($"Access denied to delete identity [{identityId}]");
                        return HandlerResult.Handled_Forbidden();
                    }
                }

            }

            return HandlerResult.NotHandled();
        }
    }
}
