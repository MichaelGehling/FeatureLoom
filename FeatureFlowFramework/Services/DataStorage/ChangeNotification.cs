using System;

namespace FeatureFlowFramework.Services.DataStorage
{
    public readonly struct ChangeNotification
    {
        public readonly string category;
        public readonly string uri;
        public readonly UpdateEvent updateEvent;
        public readonly DateTime timestamp;

        public ChangeNotification(string category, string uri, UpdateEvent updateEvent, DateTime timestamp)
        {
            this.category = category;
            this.uri = uri;
            this.updateEvent = updateEvent;
            this.timestamp = timestamp;
        }
    }
}