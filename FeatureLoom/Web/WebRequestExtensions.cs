using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using FeatureLoom.Extensions;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public static class WebRequestExtensions
    {
        public static async Task<AsyncOut<T, bool>> TryGetBodyAsync<T>(this IWebRequest request)
        {
            string body = await request.ReadAsync();
            if (body.EmptyOrNull()) return (default(T), false);
            if (body is T bodyStr) return (bodyStr, true);
            if (Json.TryParseJson(body, out T result)) return (result, true);
            return (default(T), false);
        }

        public static bool TryGetQueryItem<T>(this IWebRequest request, string key, out T item)
        {
            if (request.TryGetQueryItem(key, out string strItem))
            {
                if (Json.TryParseJson(strItem, out item)) return true;
            }

            item = default;
            return false;
        }

        public static bool TryGetCookie<T>(this IWebRequest request, string key, out T cookie)
        {
            if (request.TryGetCookie(key, out string strCookie))
            {
                if (Json.TryParseJson(strCookie, out cookie)) return true;
            }

            cookie = default;
            return false;
        }
    }
}