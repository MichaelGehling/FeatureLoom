using System.Threading.Tasks;

namespace FeatureFlowFramework.Services.Web
{
    public interface IWebServer
    {
        void Start();
        
        Task StopAsync();

        void Stop();

        bool Started { get; }

        void AddRequestHandler(IWebRequestHandler handler);

        void RemoveRequestHandler(IWebRequestHandler handler);

        void ClearRequestHandlers();

        void AddEndpoint(HttpEndpointConfig endpoint);

        void ClearEndpoints();
    }
}