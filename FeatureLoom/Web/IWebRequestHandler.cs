using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public interface IWebRequestHandler
    {
        string Route { get; }

        Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response);
    }
}