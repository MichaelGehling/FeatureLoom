using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public interface ISender
    {
        void Send<T>(in T message);

        void Send<T>(T message);

        Task SendAsync<T>(T message);
    }

    public interface ISender<T>
    {
        void Send(in T message);

        void Send(T message);

        Task SendAsync(T message);
    }
}