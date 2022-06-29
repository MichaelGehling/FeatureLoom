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

        void AddExceptionHandler(IWebExceptionHandler exceptionHandler);
        void RemoveExceptionHandler(IWebExceptionHandler exceptionHandler);
        void ClearExceptionHandlers();

        void RemoveRequestInterceptor(IWebRequestInterceptor interceptor);

        void AddResultHandler(IWebResultHandler resultHandler);

        void RemoveResultHandler(IWebResultHandler resultHandler);
        void ClearResultHandlers();

        void ClearRequestInterceptors();

        void AddEndpoint(HttpEndpointConfig endpoint);

        void ClearEndpoints();

        void SetIcon(byte[] favicon);
    }
}