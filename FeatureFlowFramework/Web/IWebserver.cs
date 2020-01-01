using System.Threading.Tasks;

namespace FeatureFlowFramework.Web
{
    public interface IWebServer
    {
        void Start();

        Task Stop();

        bool Started { get; }

        void AddRequestHandler(IWebRequestHandler handler);

        void RemoveRequestHandler(IWebRequestHandler handler);

        void ClearRequestHandlers();

        void AddEndpoint(EndpointConfig endpoint);

        void ClearEndpoints();
    }
}