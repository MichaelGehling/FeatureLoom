using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public class DistributedData<T>
    {
        SharedData<T> sharedData;
        string uri;
        DateTime timestamp = AppTime.Now;        

        public DistributedData(SharedData<T> sharedData, string uri)
        {
            this.sharedData = sharedData;
            this.uri = uri;
        }        

        public DistributedData(string uri)
        {
            this.sharedData = new SharedData<T>(default);
            this.uri = uri;            
        }

        public SharedData<T> Data => sharedData;
        public string Uri => uri;
        public DateTime Timestamp => timestamp;

    }
}
