using FeatureLoom.Collections;
using FeatureLoom.DataFlows;
using FeatureLoom.Synchronization;
using System.Threading.Tasks;

namespace FeatureLoom.Logging
{
    public class InMemoryLogger : IDataFlowSink
    {
        private CountingRingBuffer<LogMessage> buffer;

        public InMemoryLogger(int bufferSize)
        {
            buffer = new CountingRingBuffer<LogMessage>(bufferSize);
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
            return buffer.GetAvailableSince(0, out _);            
        }
    }
}