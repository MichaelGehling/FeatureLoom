﻿using FeatureLoom.Helpers;
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
        private List<ICatchHandler> catchHandlers = new List<ICatchHandler>();
        protected string route = "";
        protected Func<IWebRequest, IWebResponse, Task<HandlerResult>> handleActionAsync;
        protected string method = null;
        protected bool matchRouteExactly = false;

        protected SimpleWebRequestHandler()
        {

        }

        public SimpleWebRequestHandler(string route, Func<IWebRequest, IWebResponse, Task<HandlerResult>> handleActionAsync, string method, bool matchRouteExactly = true)
        {
            this.matchRouteExactly = matchRouteExactly;
            this.route = route ?? "";
            this.handleActionAsync = handleActionAsync;            
            this.method = method;
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
        }

        public string Route => route;

        public Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            try
            {
                if (matchRouteExactly && request.RelativePath != "") return Task.FromResult(HandlerResult.NotHandled());
                if (method != null && method != request.Method) return Task.FromResult(HandlerResult.NotHandled_MethodNotAllowed());

                if (filters.Count > 0)
                {
                    for (int i = 0; i < filters.Count; i++)
                    {
                        if (!filters[i].check(request)) return Task.FromResult(filters[i].handlerResult);
                    }
                }

                return handleActionAsync(request, response);
            }
            catch(Exception e)
            {
                foreach(var catchHandler in catchHandlers)
                {
                    var result = catchHandler.TryCatch(e, request, response);
                    if (result.requestHandled) return Task.FromResult(result);
                }
                throw;
            }
        }
        public IExtensibleWebRequestHandler AddFilter(Predicate<IWebRequest> check, HandlerResult handlerResultIfFalse)
        {
            filters.Add(new Filter(check, handlerResultIfFalse));
            return this;
        }

        public IExtensibleWebRequestHandler Catch<E>(Func<E, IWebRequest, IWebResponse, HandlerResult> reaction) where E : Exception
        {
            catchHandlers.Add(new CatchHandler<E>(reaction));
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

        private class CatchHandler<E> : ICatchHandler
        {
            public readonly Func<E, IWebRequest, IWebResponse, HandlerResult> reaction;

            public CatchHandler(Func<E, IWebRequest, IWebResponse, HandlerResult> reaction)
            {
                this.reaction = reaction;
            }

            public HandlerResult TryCatch<T>(T exception, IWebRequest request, IWebResponse response)
            {
                if (exception is E validExeption)
                {
                    return reaction(validExeption, request, response);                    
                }
                return HandlerResult.NotHandled();
            }
        }

        private interface ICatchHandler
        {
            public HandlerResult TryCatch<T>(T exception, IWebRequest request, IWebResponse response);
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