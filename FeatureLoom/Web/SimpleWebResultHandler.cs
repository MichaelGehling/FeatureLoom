using System;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class SimpleWebResultHandler : IWebResultHandler
    {
        Func<HandlerResult, IWebRequest, IWebResponse, Task> actionAsync;

        public SimpleWebResultHandler(Func<HandlerResult, IWebRequest, IWebResponse, Task> actionAsync)
        {
            this.actionAsync = actionAsync;
        }

        public Task HandleResult(HandlerResult result, IWebRequest request, IWebResponse response)
        {
            return actionAsync(result, request, response);
        }
    }
}
