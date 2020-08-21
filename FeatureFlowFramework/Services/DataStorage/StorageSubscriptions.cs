using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Services.DataStorage
{
    public class StorageSubscriptions
    {
        private Dictionary<string, Subscription> subscriptions = new Dictionary<string, Subscription>();
        private FeatureLock subscriptionsLock = new FeatureLock();

        public int Count
        {
            get
            {
                using(subscriptionsLock.LockReadOnly()) return subscriptions.Count;
            }
        }

        public void Add(string uriPattern, IDataFlowSink notificationSink)
        {
            using(subscriptionsLock.Lock())
            {
                if(!subscriptions.TryGetValue(uriPattern, out Subscription subscription))
                {                    
                    subscription = new Subscription(uriPattern, new Sender());
                    subscriptions.Add(uriPattern, subscription);
                }
                subscription.sender.ConnectTo(notificationSink);
            }            
        }

        public bool Notify(string uri, string category, UpdateEvent updateEvent)
        {
            bool notified = false;
            List<Subscription> toBeRemoved = null;
            using(subscriptionsLock.LockReadOnly())
            {
                foreach(var subscription in subscriptions.Values)
                {
                    if(subscription.sender.CountConnectedSinks == 0)
                    {
                        if(toBeRemoved == null) toBeRemoved = new List<Subscription>();
                        toBeRemoved.Add(subscription);
                    }
                    else
                    {
                        if(uri.MatchesWildcard(subscription.uriPattern))
                        {
                            var changeNotification = new ChangeNotification(category, uri, updateEvent, AppTime.Now);
                            subscription.sender.Send(changeNotification);
                            notified = true;
                        }
                    }
                }
            }

            if(toBeRemoved != null)
            {
                using(subscriptionsLock.Lock())
                {
                    foreach(var subscription in toBeRemoved)
                    {                    
                        subscriptions.Remove(subscription.uriPattern);
                    }
                }
            }

            return notified;
        }

        public void Remove(string uriPattern, IDataFlowSink notificationSink)
        {
            using(subscriptionsLock.Lock())
            {
                if(subscriptions.TryGetValue(uriPattern, out Subscription subscription))
                {
                    subscription.sender.DisconnectFrom(notificationSink);
                    if(subscription.sender.CountConnectedSinks == 0) subscriptions.Remove(uriPattern);
                }
            }
        }

        private readonly struct Subscription
        {
            public readonly string uriPattern;
            public readonly Sender sender;

            public Subscription(string uriPattern, Sender sender)
            {
                this.uriPattern = uriPattern;
                this.sender = sender;
            }
        }
    }
}
