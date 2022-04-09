using FeatureLoom.Security;
using System;
using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class SimpleWebRequestHandler : IWebRequestHandler
    {
        string permissionWildcard;
        string route = "";        
        Func<IWebRequest, IWebResponse, Task<bool>> handleActionAsync;

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<bool>> handleActionAsync, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.handleActionAsync = handleActionAsync;
            this.permissionWildcard = permissionWildcard;
        }

        public SimpleWebRequestHandler(string route, Func<string> handleAction, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction();
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, string> handleAction, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction(request);
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, string> handleAction, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction(request, response);
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<Task<string>> handleActionAsync, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync();
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, Task<string>> handleActionAsync, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync(request);
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<string>> handleActionAsync, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync(request, response);
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public string Route => route;

        public Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (permissionWildcard != null)
            {
                if (Session.Current?.Identity?.HasAnyPermission(permissionWildcard) ?? false)
                {
                    return handleActionAsync(request, response);
                }
                else
                {
                    response.StatusCode = HttpStatusCode.Forbidden;
                    return Task.FromResult(true);
                }
            }
            else return handleActionAsync(request, response);
        }
    }
}