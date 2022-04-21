using FeatureLoom.Helpers;
using FeatureLoom.Security;
using System;
using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class SimpleWebRequestHandler : IWebRequestHandler
    {
        protected string permissionWildcard;
        protected string route = "";
        protected Func<IWebRequest, IWebResponse, Task<bool>> handleActionAsync;
        protected string method = null;

        protected SimpleWebRequestHandler()
        {

        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<bool>> handleActionAsync, string method, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.handleActionAsync = handleActionAsync;
            this.permissionWildcard = permissionWildcard;
            this.method = method;
        }

        public SimpleWebRequestHandler(string route, Func<string> handleAction, string method, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction();
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, string> handleAction, string method, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction(request);
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, string> handleAction, string method, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.handleActionAsync = async (request, response) =>
            {
                string result = handleAction(request, response);
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<Task<string>> handleActionAsync, string method, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync();
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, Task<string>> handleActionAsync, string method, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.handleActionAsync = async (request, response) =>
            {
                string result = await handleActionAsync(request);
                if (result != null) await response.WriteAsync(result);
                return true;
            };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<string>> handleActionAsync, string method, string permissionWildcard = null)
        {
            this.route = route ?? "";
            this.permissionWildcard = permissionWildcard;
            this.method = method;
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
            if (method != null && method != request.Method) return Task.FromResult(false);

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

    public class SimpleWebRequestHandler<T1> : SimpleWebRequestHandler
        where T1 : IConvertible
    {
        PatternExtractor extractor;
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, Task<bool>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;            
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    return handleActionAsync(request, response, p1);
                }
                else return Task.FromResult(false);
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    string result = handleAction(p1);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    string result = handleAction(request, p1);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    string result = handleAction(request, response, p1);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    string result = await handleActionAsync(request, response, p1);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    string result = await handleActionAsync(request, p1);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    string result = await handleActionAsync(p1);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }
    }

    public class SimpleWebRequestHandler<T1, T2> : SimpleWebRequestHandler
        where T1 : IConvertible
        where T2 : IConvertible
    {
        PatternExtractor extractor;
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, Task<bool>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    return handleActionAsync(request, response, p1, p2);
                }
                else return Task.FromResult(false);
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    string result = handleAction(p1, p2);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    string result = handleAction(request, p1, p2);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    string result = handleAction(request, response, p1, p2);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    string result = await handleActionAsync(request, response, p1, p2);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    string result = await handleActionAsync(request, p1, p2);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    string result = await handleActionAsync(p1, p2);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }
    }

    public class SimpleWebRequestHandler<T1, T2, T3> : SimpleWebRequestHandler
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
    {
        PatternExtractor extractor;
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, Task<bool>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    return handleActionAsync(request, response, p1, p2, p3);
                }
                else return Task.FromResult(false);
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    string result = handleAction(p1, p2, p3);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    string result = handleAction(request, p1, p2, p3);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    string result = handleAction(request, response, p1, p2, p3);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    string result = await handleActionAsync(request, response, p1, p2, p3);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    string result = await handleActionAsync(request, p1, p2, p3);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    string result = await handleActionAsync(p1, p2, p3);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }
    }

    public class SimpleWebRequestHandler<T1, T2, T3, T4> : SimpleWebRequestHandler
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
        where T4 : IConvertible
    {
        PatternExtractor extractor;
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, T4, Task<bool>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    return handleActionAsync(request, response, p1, p2, p3, p4);
                }
                else return Task.FromResult(false);
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, T4, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    string result = handleAction(p1, p2, p3, p4);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, T4, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    string result = handleAction(request, p1, p2, p3, p4);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, T4, string> handleAction, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    string result = handleAction(request, response, p1, p2, p3, p4);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, T4, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    string result = await handleActionAsync(request, response, p1, p2, p3, p4);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, T4, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    string result = await handleActionAsync(request, p1, p2, p3, p4);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, T4, Task<string>> handleActionAsync, string method, string permissionWildcard = null) : base()
        {
            this.permissionWildcard = permissionWildcard;
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = async (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    string result = await handleActionAsync(p1, p2, p3, p4);
                    if (result != null) await response.WriteAsync(result);
                    return true;
                }
                else return false;
            };
        }
    }

}