using FeatureLoom.Collections;
using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System.Threading.Tasks;

namespace FeatureLoom.Logging
{
    public class InMemoryLogger : IMessageSink
    {
        private CircularLogBuffer<LogMessage> buffer;

        public InMemoryLogger(int bufferSize)
        {
            buffer = new CircularLogBuffer<LogMessage>(bufferSize);
        }

        public void Post<M>(in M message)
        {
            if (message is LogMessage logMessage) buffer.Add(logMessage);
        }

        public void Post<M>(M message)
        {
            Post(in message);
        }

        public Task PostAsync<M>(M message)
        {
            Post(in message);
            return Task.CompletedTask;
        }

        public LogMessage[] GetAllLogMessages()
        {
            return buffer.GetAllAvailable(0, out _);            
        }
    }
}