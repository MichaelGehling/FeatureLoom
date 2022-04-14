using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public interface ISender
    {
        void Send<T>(in T message);

        Task SendAsync<T>(T message);
    }
}