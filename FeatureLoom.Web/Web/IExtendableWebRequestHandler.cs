using FeatureLoom.Security;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IExtensibleWebRequestHandler : IWebRequestHandler
    {
        public IExtensibleWebRequestHandler AddFilter(Predicate<IWebRequest> filter, HandlerResult handlerResultIfFiltered);
        public IExtensibleWebRequestHandler HandleException<E>(Func<E, IWebRequest, IWebResponse, Task<HandlerResult>> reaction) where E : Exception;
    }

    public static class FilterableWebRequestHandlerExtensions
    {
        public static IExtensibleWebRequestHandler CheckHasPermission(this IExtensibleWebRequestHandler handler, string permission) => handler.AddFilter(_ => Session.Current?.Identity?.HasPermission(permission) ?? false, HandlerResult.Handled_Forbidden());
        public static IExtensibleWebRequestHandler CheckHasPermission(this IExtensibleWebRequestHandler handler, params string[] permissions) => handler.AddFilter(_ => Session.Current?.Identity?.HasAnyPermission(permissions) ?? false, HandlerResult.Handled_Forbidden());
        public static IExtensibleWebRequestHandler CheckMatchesPermission(this IExtensibleWebRequestHandler handler, string permissionWildcard) => handler.AddFilter(_ => Session.Current?.Identity?.MatchesAnyPermission(permissionWildcard) ?? false, HandlerResult.Handled_Forbidden());
        public static IExtensibleWebRequestHandler CheckMatchesPermission(this IExtensibleWebRequestHandler handler, params string[] permissionWildcards) => handler.AddFilter(_ => Session.Current?.Identity?.MatchesAnyPermission(permissionWildcards) ?? false, HandlerResult.Handled_Forbidden());

        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<E, IWebRequest, Task<HandlerResult>> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => reaction(e, req));
        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<E, IWebResponse, Task<HandlerResult>> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => reaction(e, resp));
        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<E, Task<HandlerResult>> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => reaction(e));
        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<Task<HandlerResult>> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => reaction());
        public static IExtensibleWebRequestHandler HandleException(this IExtensibleWebRequestHandler handler, Func<Task<HandlerResult>> reaction) => handler.HandleException((Exception e, IWebRequest req, IWebResponse resp) => reaction());

        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<E, IWebRequest, HandlerResult> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => Task.FromResult(reaction(e, req)));
        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<E, IWebResponse, HandlerResult> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => Task.FromResult(reaction(e, resp)));
        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<E, HandlerResult> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => Task.FromResult(reaction(e)));
        public static IExtensibleWebRequestHandler HandleException<E>(this IExtensibleWebRequestHandler handler, Func<HandlerResult> reaction) where E : Exception => handler.HandleException((E e, IWebRequest req, IWebResponse resp) => Task.FromResult(reaction()));
        public static IExtensibleWebRequestHandler HandleException(this IExtensibleWebRequestHandler handler, Func<HandlerResult> reaction) => handler.HandleException((Exception e, IWebRequest req, IWebResponse resp) => Task.FromResult(reaction()));

        public static IExtensibleWebRequestHandler ToExtensibleHandler(this IWebRequestHandler handler) => handler is IExtensibleWebRequestHandler extensible ? extensible : new SimpleWebRequestHandler(handler);
    }

}

