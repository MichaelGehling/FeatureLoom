using FeatureFlowFramework.DataFlows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FeatureFlowFramework.Helper;

namespace FeatureFlowFramework.Logging
{
    public class InMemoryLogger : IDataFlowSink
    {       
        CountingRingBuffer<LogMessage> buffer;
        FeatureLock bufferLock = new FeatureLock();

        public InMemoryLogger(int bufferSize)
        {
            buffer = new CountingRingBuffer<LogMessage>(bufferSize);
        }

        public void Post<M>(in M message)
        {
            using(bufferLock.ForWriting())
            {
                if(message is LogMessage logMessage) buffer.Add(logMessage);
            }
        }

        public async Task PostAsync<M>(M message)
        {
            using(await bufferLock.ForWritingAsync())
            {
                if(message is LogMessage logMessage) buffer.Add(logMessage);
            }
        }

        public LogMessage[] GetAllLogMessages()
        {
            using(bufferLock.ForReading())
            {
                return buffer.GetAvailableSince(0, out _);
            }
        }
    }
}
