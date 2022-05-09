using System;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class SimpleWebRequestInterceptor : IWebRequestInterceptor
    {
        Func<IWebRequest, IWebResponse, Task<HandlerResult>> interceptionActionAsync;

        public SimpleWebRequestInterceptor(Func<IWebRequest, IWebResponse, Task<HandlerResult>> interceptionActionAsync)
        {
            this.interceptionActionAsync = interceptionActionAsync;
        }

        public Task<HandlerResult> InterceptRequestAsync(IWebRequest request, IWebResponse response)
        {
            return interceptionActionAsync(request, response);
        }
    }
}