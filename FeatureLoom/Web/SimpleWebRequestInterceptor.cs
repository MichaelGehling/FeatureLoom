using System;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class SimpleWebRequestInterceptor : IWebRequestInterceptor
    {
        Func<IWebRequest, IWebResponse, Task<bool>> interceptionActionAsync;

        public SimpleWebRequestInterceptor(Func<IWebRequest, IWebResponse, Task<bool>> interceptionActionAsync)
        {
            this.interceptionActionAsync = interceptionActionAsync;
        }

        public Task<bool> InterceptRequestAsync(IWebRequest request, IWebResponse response)
        {
            return interceptionActionAsync(request, response);
        }
    }
}