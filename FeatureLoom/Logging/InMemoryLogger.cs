using FeatureLoom.Collections;
using FeatureLoom.DataFlows;
using FeatureLoom.Synchronization;
using System.Threading.Tasks;

namespace FeatureLoom.Logging
{
    public class InMemoryLogger : IDataFlowSink
    {
        private CountingRingBuffer<LogMessage> buffer;
        private MicroLock bufferLock = new MicroLock();

        public InMemoryLogger(int bufferSize)
        {
            buffer = new CountingRingBuffer<LogMessage>(bufferSize);
        }

        public void Post<M>(in M message)
        {
            if (message is LogMessage logMessage) using (bufferLock.Lock()) buffer.Add(logMessage);
        }

        public void Post<M>(M message)
        {
            if (message is LogMessage logMessage) using (bufferLock.Lock()) buffer.Add(logMessage);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is LogMessage logMessage) using (bufferLock.Lock()) buffer.Add(logMessage);
            return Task.CompletedTask;
        }

        public LogMessage[] GetAllLogMessages()
        {
            using (bufferLock.Lock())
            {
                return buffer.GetAvailableSince(0, out _);
            }
        }
    }
}