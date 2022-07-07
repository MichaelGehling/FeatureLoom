using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using FeatureLoom.Services;

namespace FeatureLoom.Helpers
{
    public static class TestHelper
    {
        private class ContextData
        {
            public FeatureLock contextLock = new FeatureLock();

            public List<LogMessage> errors = new List<LogMessage>();
            public List<LogMessage> warnings = new List<LogMessage>();
        }

        public static void PrepareTestContext(bool disconnectLoggers = true, bool useMemoryStorage = true, bool bufferLogErrorsAndWarnings = true)
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            ServiceContext.UseNewContexts();
            if (disconnectLoggers)
            {
                Log.QueuedLogSource.DisconnectAll();
                Log.SyncLogSource.DisconnectAll();
            }

            Log.SyncLogSource.ProcessMessage<LogMessage>(msg =>
            {
                var contextData = Service<ContextData>.Instance;
                using (contextData.contextLock.Lock())
                {
                    if (msg.level == Loglevel.ERROR) contextData.errors.Add(msg);
                    else if (bufferLogErrorsAndWarnings && msg.level == Loglevel.WARNING) contextData.warnings.Add(msg);
                }
            });

            if (useMemoryStorage)
            {
                Storage.DefaultReaderFactory = (category) => new MemoryStorage(category);
                Storage.DefaultWriterFactory = (category) => new MemoryStorage(category);
                Storage.RemoveAllReaderAndWriter();
            }            
        }

        public static bool HasAnyLogError(bool includeWarnings = true)
        {
            var contextData = Service<ContextData>.Instance;
            using (contextData.contextLock.LockReadOnly())
            {
                if (includeWarnings) return contextData.errors.Count > 0 || contextData.warnings.Count > 0;
                else return contextData.errors.Count > 0;
            }
        }

        public static LogMessage[] LogErrors
        {
            get
            {
                var contextData = Service<ContextData>.Instance;
                using (contextData.contextLock.LockReadOnly()) return contextData.errors.ToArray();
            }
        }

        public static LogMessage[] LogWarnings
        {
            get
            {
                var contextData = Service<ContextData>.Instance;
                using (contextData.contextLock.LockReadOnly()) return contextData.warnings.ToArray();
            }
        }

       
    }
}