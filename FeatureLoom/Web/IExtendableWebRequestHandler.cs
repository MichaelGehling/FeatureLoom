using FeatureLoom.Security;
using System;

namespace FeatureLoom.Web
{
    public interface IExtensibleWebRequestHandler : IWebRequestHandler
    {
        public IExtensibleWebRequestHandler AddFilter(Predicate<IWebRequest> filter, HandlerResult handlerResultIfFiltered);
        public IExtensibleWebRequestHandler Catch<E>(Func<E, IWebRequest, IWebResponse, HandlerResult> reaction) where E : Exception;
    }

    public static class FilterableWebRequestHandlerExtensions
    {
        public static IExtensibleWebRequestHandler CheckHasPermission(this IExtensibleWebRequestHandler handler, string permission) => handler.AddFilter(_ => Session.Current?.Identity?.HasPermission(permission) ?? false, HandlerResult.Handled_Forbidden());
        public static IExtensibleWebRequestHandler CheckHasPermission(this IExtensibleWebRequestHandler handler, params string[] permissions) => handler.AddFilter(_ => Session.Current?.Identity?.HasAnyPermission(permissions) ?? false, HandlerResult.Handled_Forbidden());
        public static IExtensibleWebRequestHandler CheckMatchesPermission(this IExtensibleWebRequestHandler handler, string permissionWildcard) => handler.AddFilter(_ => Session.Current?.Identity?.MatchesAnyPermission(permissionWildcard) ?? false, HandlerResult.Handled_Forbidden());
        public static IExtensibleWebRequestHandler CheckMatchesPermission(this IExtensibleWebRequestHandler handler, params string[] permissionWildcards) => handler.AddFilter(_ => Session.Current?.Identity?.MatchesAnyPermission(permissionWildcards) ?? false, HandlerResult.Handled_Forbidden());

        public static IExtensibleWebRequestHandler Catch<E>(this IExtensibleWebRequestHandler handler, Func<E, IWebRequest, HandlerResult> reaction) where E : Exception => handler.Catch((E e, IWebRequest req, IWebResponse resp) => reaction(e, req));
        public static IExtensibleWebRequestHandler Catch<E>(this IExtensibleWebRequestHandler handler, Func<E, IWebResponse, HandlerResult> reaction) where E : Exception => handler.Catch((E e, IWebRequest req, IWebResponse resp) => reaction(e, resp));
        public static IExtensibleWebRequestHandler Catch<E>(this IExtensibleWebRequestHandler handler, Func<E, HandlerResult> reaction) where E : Exception => handler.Catch((E e, IWebRequest req, IWebResponse resp) => reaction(e));
        public static IExtensibleWebRequestHandler Catch<E>(this IExtensibleWebRequestHandler handler, Func<HandlerResult> reaction) where E : Exception => handler.Catch((E e, IWebRequest req, IWebResponse resp) => reaction());
        public static IExtensibleWebRequestHandler Catch(this IExtensibleWebRequestHandler handler, Func<HandlerResult> reaction) => handler.Catch((Exception e, IWebRequest req, IWebResponse resp) => reaction());        
    }

}

