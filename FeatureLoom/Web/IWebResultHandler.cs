using System;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebResultHandler
    {
        Task HandleResult(HandlerResult result, IWebRequest request, IWebResponse response);
    }
}
