using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebRequestHandler
    {
        string Route { get; }
        Task<HandlerResult> HandleRequestAsync(IWebRequest request, IWebResponse response);
        string[] SupportedMethods { get; }
        bool RouteMustMatchExactly { get; }
    }
}