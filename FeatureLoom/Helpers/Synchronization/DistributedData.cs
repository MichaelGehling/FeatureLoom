using FeatureLoom.DataFlows;
using FeatureLoom.Helpers.Extensions;
using FeatureLoom.Services;
using FeatureLoom.Services.Logging;
using FeatureLoom.Services.MetaData;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers.Synchronization
{
    public class DistributedData<T>
    {
        SharedData<T> sharedData;
        string uri;
        DateTime timestamp = AppTime.Now;
        Sender<DistributedDataUpdate> updateSender = new Sender<DistributedDataUpdate>();
        public IDataFlowSource UpdateSender => updateSender;
        ProcessingEndpoint<DistributedDataUpdate> updateProcessor;
        public IDataFlowSink UpdateReceiver => updateProcessor;
        ProcessingEndpoint<SharedDataUpdateNotification> changePublisher;
        long originatorId = RandomGenerator.Int64();

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

        bool ProcessUpdate(DistributedDataUpdate update)
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
                            value = update.serializedData.FromJson<T>(); 
                            data.SetValue(value);
                            this.timestamp = update.timestamp;
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.ERROR(this.GetHandle(), $"Failed deserializing data from distribution! Uri={uri}", e.ToString());
                }
            }
            return false;
        }

        bool PublishChange(SharedDataUpdateNotification change)
        {
            if (change.originatorId != this.originatorId && updateSender.CountConnectedSinks > 0)
            {
                try
                {
                    using (var data = sharedData.GetReadAccess())
                    {
                        this.timestamp = AppTime.Now;
                        DistributedDataUpdate update = new DistributedDataUpdate(data.Value.ToJson(), uri, this.timestamp);
                        updateSender.Send(update);
                        return true;
                    }
                }
                catch(Exception e)
                {
                    Log.ERROR(this.GetHandle(), $"Failed serializing data for distribution! Uri={uri}", e.ToString());
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
