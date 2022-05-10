using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebRequestInterceptor
    {        
        Task<HandlerResult> InterceptRequestAsync(IWebRequest request, IWebResponse response);
    }

    public interface IWebExceptionHandler
    {
        Task<HandlerResult> HandleException(Exception e, IWebRequest request, IWebResponse response);
    }
}
