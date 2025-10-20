using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System.Collections.Generic;

namespace FeatureLoom.Storages
{
    public class StorageSubscriptions
    {
        private Dictionary<string, Subscription> subscriptions = new Dictionary<string, Subscription>();
        private FeatureLock subscriptionsLock = new FeatureLock();

        public int Count
        {
            get
            {
                using (subscriptionsLock.LockReadOnly()) return subscriptions.Count;
            }
        }

        public void Add(string uriPattern, IMessageSink<ChangeNotification> notificationSink)
        {
            using (subscriptionsLock.Lock())
            {
                if (!subscriptions.TryGetValue(uriPattern, out Subscription subscription))
                {
                    subscription = new Subscription(uriPattern, new Sender<ChangeNotification>());
                    subscriptions.Add(uriPattern, subscription);
                }
                subscription.sender.ConnectTo(notificationSink);
            }
        }

        public bool Notify(string uri, string category, UpdateEvent updateEvent)
        {
            bool notified = false;
            List<Subscription> toBeRemoved = null;
            using (subscriptionsLock.LockReadOnly())
            {
                foreach (var subscription in subscriptions.Values)
                {
                    if (subscription.sender.CountConnectedSinks == 0)
                    {
                        if (toBeRemoved == null) toBeRemoved = new List<Subscription>();
                        toBeRemoved.Add(subscription);
                    }
                    else
                    {
                        if (uri.MatchesWildcardPattern(subscription.uriPattern))
                        {
                            var changeNotification = new ChangeNotification(category, uri, updateEvent, AppTime.Now);
                            subscription.sender.Send(changeNotification);
                            notified = true;
                        }
                    }
                }
            }

            if (toBeRemoved != null)
            {
                using (subscriptionsLock.Lock())
                {
                    foreach (var subscription in toBeRemoved)
                    {
                        subscriptions.Remove(subscription.uriPattern);
                    }
                }
            }

            return notified;
        }

        public void Remove(string uriPattern, IMessageSink<ChangeNotification> notificationSink)
        {
            using (subscriptionsLock.Lock())
            {
                if (subscriptions.TryGetValue(uriPattern, out Subscription subscription))
                {
                    subscription.sender.DisconnectFrom(notificationSink);
                    if (subscription.sender.CountConnectedSinks == 0) subscriptions.Remove(uriPattern);
                }
            }
        }

        private readonly struct Subscription
        {
            public readonly string uriPattern;
            public readonly Sender<ChangeNotification> sender;

            public Subscription(string uriPattern, Sender<ChangeNotification> sender)
            {
                this.uriPattern = uriPattern;
                this.sender = sender;
            }
        }
    }
}