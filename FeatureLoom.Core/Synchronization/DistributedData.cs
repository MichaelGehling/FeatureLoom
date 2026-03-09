using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Extensions;
using FeatureLoom.Serialization;
using FeatureLoom.Time;
using System;

namespace FeatureLoom.Synchronization
{
    public class DistributedData<T>
    {
        private SharedData<T> sharedData;
        private string uri;
        private DateTime timestamp = AppTime.Now;
        private Sender<DistributedDataUpdate> updateSender = new Sender<DistributedDataUpdate>();
        public IMessageSource UpdateSender => updateSender;
        private ProcessingEndpoint<DistributedDataUpdate> updateProcessor;
        public IMessageSink UpdateReceiver => updateProcessor;
        private ProcessingEndpoint<SharedDataUpdateNotification> changePublisher;
        private long originatorId = RandomGenerator.Int64();

        public DistributedData(SharedData<T> sharedData, string uri)
        {
            Init(sharedData, uri);
        }

        public DistributedData(string uri)
        {
            Init(new SharedData<T>(default), uri);
        }

        private void Init(SharedData<T> sharedData, string uri)
        {
            this.sharedData = sharedData;
            this.uri = uri;
            updateProcessor = new ProcessingEndpoint<DistributedDataUpdate>(ProcessUpdate);
            changePublisher = new ProcessingEndpoint<SharedDataUpdateNotification>(PublishChange);
            sharedData.UpdateNotifications.ConnectTo(changePublisher);
        }

        private bool ProcessUpdate(DistributedDataUpdate update)
        {
            if (update.uri == this.uri)
            {
                try
                {
                    using (var data = sharedData.GetWriteAccess(this.originatorId))
                    {
                        if (update.timestamp > this.timestamp)
                        {
                            var value = data.Value;
                            if (!JsonHelper.DefaultDeserializer.TryDeserialize(update.serializedData, out value)) throw new Exception("Failed on deserializing");
                            data.SetValue(value);
                            this.timestamp = update.timestamp;
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build($"Failed deserializing data from distribution! Uri={uri}", e);
                }
            }
            return false;
        }

        private bool PublishChange(SharedDataUpdateNotification change)
        {
            if (change.originatorId != this.originatorId && updateSender.CountConnectedSinks > 0)
            {
                try
                {
                    using (var data = sharedData.GetReadAccess())
                    {
                        this.timestamp = AppTime.Now;
                        string json = JsonHelper.DefaultSerializer.Serialize(data.Value);
                        DistributedDataUpdate update = new DistributedDataUpdate(json, uri, this.timestamp);
                        updateSender.Send(update);
                        return true;
                    }
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build($"Failed serializing data for distribution! Uri={uri}", e);
                }
            }
            return false;
        }

        public SharedData<T> Data => sharedData;
        public string Uri => uri;
        public DateTime Timestamp => timestamp;
    }

    public class DistributedDataUpdate
    {
        public readonly string serializedData;
        public readonly string uri;
        public readonly DateTime timestamp;

        public DistributedDataUpdate(string serializedData, string uri, DateTime timestamp)
        {
            this.serializedData = serializedData;
            this.uri = uri;
            this.timestamp = timestamp;
        }
    }
}