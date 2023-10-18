using FeatureLoom.Helpers;
using FeatureLoom.Security;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{

    public class SimpleWebRequestHandler : IExtensibleWebRequestHandler
    {
        private List<Filter> filters = new List<Filter>();
        private List<IWebExceptionHandler> exceptionHandlers = new List<IWebExceptionHandler>();
        protected string route = "";
        protected Func<IWebRequest, IWebResponse, Task<HandlerResult>> handleActionAsync;
        protected string method = null;
        protected bool matchRouteExactly = false;

        string[] supportedMethods;
        public string[] SupportedMethods => supportedMethods;

        public bool RouteMustMatchExactly => matchRouteExactly;

        protected SimpleWebRequestHandler()
        {

        }

        // Use SimpleWebRequestHandler as a wrapper for any IWebRequestHandler to make it an IExtensibleWebRequestHandler
        public SimpleWebRequestHandler(IWebRequestHandler handler)
        {
            this.matchRouteExactly = handler.RouteMustMatchExactly;
            this.route = handler.Route;
            this.handleActionAsync = handler.HandleRequestAsync;
            this.supportedMethods = handler.SupportedMethods;
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<HandlerResult>> handleActionAsync, string method, bool matchRouteExactly = true)
        {
            this.matchRouteExactly = matchRouteExactly;
            this.route = route ?? "";
            this.handleActionAsync = handleActionAsync;            
            this.method = method;
            this.supportedMethods = method != null ? new string[] { method } : new string[] { };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, HandlerResult> handleAction, string method, bool matchRouteExactly = true)
        {
            this.route = route ?? "";
            this.matchRouteExactly = matchRouteExactly;
            this.method = method;
            this.handleActionAsync = (request, response) =>
            {
                return Task.FromResult(handleAction(request)); 
            };
            this.supportedMethods = method != null ? new string[] { method } : new string[] { };
        }

        public SimpleWebRequestHandler(string route, Func<HandlerResult> handleAction, string method, bool matchRouteExactly = true)
        {
            this.route = route ?? "";
            this.matchRouteExactly = matchRouteExactly;
            this.method = method;
            this.handleActionAsync = (request, response) =>
            {
                return Task.FromResult(handleAction());
            };
            this.supportedMethods = method != null ? new string[] { method } : new string[] { };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, HandlerResult> handleAction, string method, bool matchRouteExactly = true)
        {
            this.route = route ?? "";
            this.matchRouteExactly = matchRouteExactly;
            this.method = method;
            this.handleActionAsync = (request, response) =>
            {
                return Task.FromResult(handleAction(request, response));                
            };
            this.supportedMethods = method != null ? new string[] { method } : new string[] { };
        }

        public SimpleWebRequestHandler(string route, Func<Task<HandlerResult>> handleActionAsync, string method, bool matchRouteExactly = true)
        {
            this.route = route ?? "";
            this.matchRouteExactly = matchRouteExactly;
            this.method = method;
            this.handleActionAsync = (request, response) =>
            {
                return handleActionAsync();
            };
            this.supportedMethods = method != null ? new string[] { method } : new string[] { };
        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, Task<HandlerResult>> handleActionAsync, string method, bool matchRouteExactly = true)
        {
            this.route = route ?? "";
            this.matchRouteExactly = matchRouteExactly;
            this.method = method;
            this.handleActionAsync = (request, response) =>
            {
                return handleActionAsync(request);
            };
            this.supportedMethods = method != null ? new string[] { method } : new string[] { };
        }

        public string Route => route;

        public async Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            try
            {
                if (matchRouteExactly && request.RelativePath != "") return HandlerResult.NotHandled();
                if (method != null && method != request.Method) return HandlerResult.NotHandled_MethodNotAllowed();

                if (filters.Count > 0)
                {
                    for (int i = 0; i < filters.Count; i++)
                    {
                        if (!filters[i].check(request)) return filters[i].handlerResult;
                    }
                }

                return await handleActionAsync(request, response);
            }
            catch(Exception e)
            {
                foreach(var exceptionHandler in exceptionHandlers)
                {                    
                    var result = await exceptionHandler.HandleException(e, request, response);
                    if (result.requestHandled) return result;
                }
                throw;
            }
        }
        public IExtensibleWebRequestHandler AddFilter(Predicate<IWebRequest> check, HandlerResult handlerResultIfFalse)
        {
            filters.Add(new Filter(check, handlerResultIfFalse));
            return this;
        }

        public IExtensibleWebRequestHandler HandleException<E>(Func<E, IWebRequest, IWebResponse, Task<HandlerResult>> reaction) where E : Exception
        {
            exceptionHandlers.Add(new SimpleWebExceptionHandler<E>(reaction));
            return this;
        }

        private readonly struct Filter
        {
            public readonly Predicate<IWebRequest> check;
            public readonly HandlerResult handlerResult;

            public Filter(Predicate<IWebRequest> check, HandlerResult handlerResult)
            {
                this.check = check;
                this.handlerResult = handlerResult;
            }
        }
    }

    public class SimpleWebRequestHandler<T1> : SimpleWebRequestHandler
        where T1 : IConvertible
    {
        PatternExtractor extractor;
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, Task<HandlerResult>> handleActionAsync, string method) : base()
        {            
            this.method = method;            
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    return handleActionAsync(request, response, p1);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    return Task.FromResult(handleAction(p1));                    
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    return Task.FromResult(handleAction(request, p1));                    
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    return Task.FromResult(handleAction(request, response, p1));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    return handleActionAsync(request, p1);                    
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1))
                {
                    return handleActionAsync(p1);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }
    }

    public class SimpleWebRequestHandler<T1, T2> : SimpleWebRequestHandler
        where T1 : IConvertible
        where T2 : IConvertible
    {
        PatternExtractor extractor;
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    return handleActionAsync(request, response, p1, p2);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    return Task.FromResult(handleAction(p1, p2));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    return Task.FromResult(handleAction(request, p1, p2));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    return Task.FromResult(handleAction(request, response, p1, p2));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    return handleActionAsync(request, p1, p2);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2))
                {
                    return handleActionAsync(p1, p2);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }
    }

    public class SimpleWebRequestHandler<T1, T2, T3> : SimpleWebRequestHandler
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
    {
        PatternExtractor extractor;
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    return handleActionAsync(request, response, p1, p2, p3);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    return Task.FromResult(handleAction(p1, p2, p3));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    return Task.FromResult(handleAction(request, p1, p2, p3));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    return Task.FromResult(handleAction(request, response, p1, p2, p3));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    return handleActionAsync(request, p1, p2, p3);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3))
                {
                    return handleActionAsync(p1, p2, p3);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
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
        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, T4, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    return handleActionAsync(request, response, p1, p2, p3, p4);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, T4, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    return Task.FromResult(handleAction(p1, p2, p3, p4));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, T4, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    return Task.FromResult(handleAction(request, p1, p2, p3, p4));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, IWebResponse, T1, T2, T3, T4, HandlerResult> handleAction, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);
            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    return Task.FromResult(handleAction(request, response, p1, p2, p3, p4));
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<IWebRequest, T1, T2, T3, T4, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    return handleActionAsync(request, p1, p2, p3, p4);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }

        public SimpleWebRequestHandler(string routePattern, Func<T1, T2, T3, T4, Task<HandlerResult>> handleActionAsync, string method) : base()
        {
            
            this.method = method;
            this.extractor = new PatternExtractor(routePattern, out route, true);

            this.handleActionAsync = (request, response) =>
            {
                if (extractor.TryExtract(request.RelativePath, out T1 p1, out T2 p2, out T3 p3, out T4 p4))
                {
                    return handleActionAsync(p1, p2, p3, p4);
                }
                else return Task.FromResult(HandlerResult.NotHandled_BadRequest());
            };
        }
    }
}