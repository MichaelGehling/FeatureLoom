﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using FeatureLoom.Services;

namespace FeatureLoom.Diagnostics
{
    public static class TestHelper
    {
        private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static void PrepareTestContext(bool disconnectLoggers = true, bool useMemoryStorage = true, bool bufferLogErrorsAndWarnings = true)
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();
            ServiceContext.UseNewContexts();
            if (disconnectLoggers)
            {
                Log.QueuedLogSource.DisconnectAll();
                Log.SyncLogSource.DisconnectAll();
            }

            Log.SyncLogSource.ConnectTo(new ProcessingEndpoint<LogMessage>(msg =>
            {
                using (context.Data.contextLock.Lock())
                {
                    if (msg.level == Loglevel.ERROR) context.Data.errors.Add(msg);
                    else if (bufferLogErrorsAndWarnings && msg.level == Loglevel.WARNING) context.Data.warnings.Add(msg);
                }
            }));

            if (useMemoryStorage)
            {
                Storage.DefaultReaderFactory = (category) => new MemoryStorage(category);
                Storage.DefaultWriterFactory = (category) => new MemoryStorage(category);
                Storage.RemoveAllReaderAndWriter();
            }            
        }

        public static bool HasAnyLogError(bool includeWarnings = true)
        {
            using (context.Data.contextLock.LockReadOnly())
            {
                if (includeWarnings) return context.Data.errors.Count > 0 || context.Data.warnings.Count > 0;
                else return context.Data.errors.Count > 0;
            }
        }

        public static LogMessage[] LogErrors
        {
            get
            {
                using (context.Data.contextLock.LockReadOnly()) return context.Data.errors.ToArray();
            }
        }

        public static LogMessage[] LogWarnings
        {
            get
            {
                using (context.Data.contextLock.LockReadOnly()) return context.Data.warnings.ToArray();
            }
        }

        private class ContextData : IServiceContextData
        {
            public FeatureLock contextLock = new FeatureLock();

            public List<LogMessage> errors = new List<LogMessage>();
            public List<LogMessage> warnings = new List<LogMessage>();

            public IServiceContextData Copy()
            {
                var copy = new ContextData();
                copy.errors.AddRange(this.errors);
                copy.warnings.AddRange(this.warnings);
                return copy;
            }
        }
    }
}