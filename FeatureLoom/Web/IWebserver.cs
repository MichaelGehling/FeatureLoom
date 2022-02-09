using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebServer
    {
        Task Run();

        Task StopAsync();

        void Stop();

        bool Running { get; }

        void AddRequestHandler(IWebRequestHandler handler);

        void RemoveRequestHandler(IWebRequestHandler handler);

        void ClearRequestHandlers();
        void AddRequestInterceptor(IWebRequestInterceptor interceptor);

        void RemoveRequestInterceptor(IWebRequestInterceptor interceptor);

        void ClearRequestInterceptors();

        void AddEndpoint(HttpEndpointConfig endpoint);

        void ClearEndpoints();

        void SetIcon(byte[] favicon);
    }
}