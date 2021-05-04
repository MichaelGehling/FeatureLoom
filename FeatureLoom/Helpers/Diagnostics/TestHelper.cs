using FeatureLoom.DataFlows;
using FeatureLoom.Helpers.Misc;
using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Services.DataStorage;
using FeatureLoom.Services.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers.Diagnostics
{
    public static class TestHelper
    {
        static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static void PrepareTestContext(bool disconnectLoggers = true, bool useMemoryStorage = true, bool bufferLogErrorsAndWarnings = true)
        {
            ServiceContext.UseNewContexts();
            if(disconnectLoggers) Log.LogForwarder.DisconnectAll();
            if(useMemoryStorage)
            {
                Storage.DefaultReaderFactory = (category) => new MemoryStorage(category);
                Storage.DefaultWriterFactory = (category) => new MemoryStorage(category);
                Storage.RemoveAllReaderAndWriter();
            }
            if(bufferLogErrorsAndWarnings)
            {
                Log.LogForwarder.ConnectTo(new ProcessingEndpoint<LogMessage>(msg =>
                {
                    using(context.Data.contextLock.Lock())
                    {
                        if(msg.level == Loglevel.ERROR) context.Data.errors.Add(msg);
                        else if(msg.level == Loglevel.WARNING) context.Data.warnings.Add(msg);
                    }
                }));
            }
        }

        public static bool HasAnyLogError(bool includeWarnings = true)
        {
            using(context.Data.contextLock.LockReadOnly())
            {
                if(includeWarnings) return context.Data.errors.Count > 0 || context.Data.warnings.Count > 0;
                else return context.Data.errors.Count > 0;
            }
        }

        public static LogMessage[] LogErrors
        {
            get
            {
                using(context.Data.contextLock.LockReadOnly()) return context.Data.errors.ToArray();
            }
        }

        public static LogMessage[] LogWarnings
        {
            get
            {
                using(context.Data.contextLock.LockReadOnly()) return context.Data.warnings.ToArray();
            }
        }

        class ContextData : IServiceContextData
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
