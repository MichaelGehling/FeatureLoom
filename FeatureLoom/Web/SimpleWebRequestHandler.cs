using System;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class SimpleWebRequestHandler : IWebRequestHandler
    {
        string route = "";        
        Func<IWebRequest, IWebResponse, Task<bool>> handleActionAsync;

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<bool>> handleActionAsync)
        {
            this.route = route ?? "";
            this.handleActionAsync = handleActionAsync;
        }

        public SimpleWebRequestHandler(string route, Func<string> handleAction)
        {
            this.route = route ?? "";
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction();
                await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, string> handleAction)
        {
            this.route = route ?? "";
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction(request);
                await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, string> handleAction)
        {
            this.route = route ?? "";
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction(request, response);
                await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<Task<string>> handleActionAsync)
        {
            this.route = route ?? "";
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync();
                await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, Task<string>> handleActionAsync)
        {
            this.route = route ?? "";
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync(request);
                await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<string>> handleActionAsync)
        {
            this.route = route ?? "";
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync(request, response);
                await response.WriteAsync(result);
                return true;
            };
        }

        public string Route => route;

        public Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            return handleActionAsync(request, response);
        }
    }
}