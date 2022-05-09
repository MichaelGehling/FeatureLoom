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

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            Session session = Session.Current;
            if (session?.Identity == null) HandlerResult.Handled_Forbidden();            

            var splits = request.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);            

            if (splits.Length == 2 && splits[0] == "Identity")
            {
                string identityId = splits[1];

                if (request.IsDelete)
                {                    
                    if (identityId == session.IdentityId)
                    {
                        bool failed = false;
                        // Delete own identity (also possible without extra access right)
                        if (await session.Identity.TryRemoveFromStorageAsync())
                        {
                            Log.INFO(this.GetHandle(), $"Successfully deleted identity [{identityId}]");                            
                        }
                        else
                        {
                            Log.ERROR(this.GetHandle(), $"Failed deleting identity [{identityId}]");
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
                            Log.INFO(this.GetHandle(), $"Could not delete identity [{identityId}], because it does not exist.");
                            return HandlerResult.Handled_NotFound();
                        }
                        else if (await Storage.GetWriter(Identity.StorageCategory).TryDeleteAsync(identityId))
                        {
                            Log.INFO(this.GetHandle(), $"Successfully deleted identity [{identityId}]");
                            return HandlerResult.Handled_OK();
                        }
                        else
                        {
                            Log.ERROR(this.GetHandle(), $"Failed deleting identity [{identityId}]");
                            return HandlerResult.Handled_InternalServerError();
                        }
                        
                    }
                    else
                    {
                        Log.WARNING(this.GetHandle(), $"Access denied to delete identity [{identityId}]");
                        return HandlerResult.Handled_Forbidden();
                    }
                }

            }

            return HandlerResult.NotHandled();
        }
    }
}
