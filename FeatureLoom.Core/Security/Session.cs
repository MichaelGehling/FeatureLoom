using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Scheduling;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Security
{
    public class Session
    {
        #region static
        static ThreadLocal<Session> currentSession = new ThreadLocal<Session>();
        static string storageCategory = "Security/Sessions";
        static TimeSpan defaultTimeout = 1.Hours();
        static TimeSpan refreshThreshold = 10.Seconds();
        public static TimeSpan RefreshThreshold { get => refreshThreshold; set => refreshThreshold = value; }

        static TimeSpan cleanupInterval = 10.Minutes();
        static DateTime lastCleanup = AppTime.Now;
        static FeatureLock cleanupLock = new FeatureLock();
        static ISchedule cleanupSchedule = StartCleanupSchedule();

        public static void StopCleanupSchedule() => cleanupSchedule = null;

        public static ISchedule StartCleanupSchedule()
        {
            cleanupSchedule = Scheduler.ScheduleAction(now =>
            {
                if (!cleanupLock.IsLocked)
                {
                    TimeFrame timer = new TimeFrame(lastCleanup, cleanupInterval);
                    if (timer.Elapsed(now))
                    {
                        _ = CleanUpAsync();
                    }
                    else
                    {
                        return (cleanupSchedule != null, timer.Remaining(now));
                    }
                }
                return (cleanupSchedule != null, cleanupInterval);
            });

            return cleanupSchedule;
        }        

        public static async Task CleanUpAsync()
        {
            if (cleanupLock.TryLock(out var lockHandle))
            {
                using (lockHandle)
                {
                    var reader = Storage.GetReader(storageCategory);
                    var writer = Storage.GetWriter(storageCategory);

                    if ((await reader.TryListUrisAsync()).Out(out var uris))
                    {
                        foreach (string uri in uris)
                        {
                            if ((await reader.TryReadAsync<Session>(uri)).Out(out Session session))
                            {
                                if (session.LifeTime.Elapsed()) await writer.TryDeleteAsync(uri);
                            }
                        }
                    }
                }
            }            
        }

        public static Session Current { get => currentSession.IsValueCreated ? currentSession.Value : null; set => currentSession.Value = value; }        
        public static string StorageCategory { get => storageCategory; set => storageCategory = value; }
        public static TimeSpan DefaultTimeout { get => defaultTimeout; set => defaultTimeout = value; }
        public static Task<AsyncOut<bool, Session>> TryLoadSessionAsync(string sessionId)
        {
            return Storage.GetReader(storageCategory).TryReadAsync<Session>(sessionId);
        }        
        static string CreateNewSessionId() => RandomGenerator.GUID(true).ToString();
        
        #endregion static

        public Session(Identity identity)
        {
            this.sessionId = CreateNewSessionId();
            this.identityId = identity.IdentityId;
            this.timeout = defaultTimeout;

            creationTime = AppTime.CoarseNow;
            lastRefresh = creationTime;
        }

        public Session()
        {
        }

        [JsonIgnore]
        Identity identity;
        string identityId;

        string sessionId;
        DateTime creationTime;
        DateTime lastRefresh;
        TimeSpan timeout;
        string trackerId;

        [JsonIgnore]
        public string SessionId => sessionId;
        [JsonIgnore]
        public string IdentityId { get => identityId; }
        [JsonIgnore]
        public Identity Identity
        {
            get
            {
                if (this.identity == null)
                {
                    if (Identity.TryLoadIdentityAsync(identityId).WaitFor().Out(out Identity identity))
                    {
                        this.identity = identity;
                    }
                    else
                    {
                        throw new Exception($"Identity with ID {identityId} cannot be loaded.");
                    }
                }
                return this.identity;
            }
        }

        [JsonIgnore]
        public DateTime CreationTime => creationTime;

        [JsonIgnore]
        public DateTime LastRefresh => lastRefresh;

        [JsonIgnore]
        public TimeSpan Timeout => timeout;
        [JsonIgnore]
        public TimeFrame LifeTime => new TimeFrame(creationTime, lastRefresh + timeout);

        public string TrackerId { get => trackerId; set => trackerId = value; }

        public bool Refresh(bool force = false)
        {
            var newTime = AppTime.CoarseNow;
            if (force || newTime - lastRefresh > refreshThreshold)
            {
                lastRefresh = newTime;
                _ = TryStoreAsync();
                return true;
            }
            else return false;
        }

        public Task<bool> TryStoreAsync()
        {
            return Storage.GetWriter(storageCategory).TryWriteAsync(sessionId, this);
        }

        public Task<bool> TryDeleteFromStorageAsync()
        {
            return Storage.GetWriter(storageCategory).TryDeleteAsync(sessionId);
        }
    }
}
