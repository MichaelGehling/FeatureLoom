using System;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebExceptionHandler
    {
        Task<HandlerResult> HandleException(Exception e, IWebRequest request, IWebResponse response);
    }
}
