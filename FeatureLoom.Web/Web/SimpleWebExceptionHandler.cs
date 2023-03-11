using System;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class SimpleWebExceptionHandler<E> : IWebExceptionHandler where E:Exception
    {
        Func<E, IWebRequest, IWebResponse, Task<HandlerResult>> actionAsync;

        public SimpleWebExceptionHandler(Func<E, IWebRequest, IWebResponse, Task<HandlerResult>> actionAsync)
        {
            this.actionAsync = actionAsync;
        }

        public Task<HandlerResult> HandleException(Exception e, IWebRequest request, IWebResponse response)
        {
            if (e is E typedException) return actionAsync(typedException, request, response);
            else return Task.FromResult(HandlerResult.NotHandled());
        }
    }
}
