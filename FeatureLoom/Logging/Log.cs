using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.MetaDatas;
using FeatureLoom.Workflows;
using System;

namespace FeatureLoom.Logging
{
    public static class Log
    {
        public static ConsoleLogger defaultConsoleLogger = new ConsoleLogger();
        public static FileLogger defaultFileLogger = new FileLogger();
        public static IWorkflowRunner logRunner = new SuspendingAsyncRunner();

        static Log()
        {
            WorkflowRunnerService.Unregister(logRunner);
            logRunner.Run(defaultFileLogger);
        }

        private class ContextData : IServiceContextData
        {
            public readonly Forwarder<LogMessage> logSink = new Forwarder<LogMessage>();
            public readonly QueueForwarder<LogMessage> queueLogForwarder = new QueueForwarder<LogMessage>(1, 1000, 10, 10000, TimeSpan.Zero, true);
            public readonly Forwarder<LogMessage> syncLogForwarder = new Forwarder<LogMessage>();

            public ContextData()
            {
                logSink.ConnectTo(queueLogForwarder);
                logSink.ConnectTo(syncLogForwarder);
                queueLogForwarder.ConnectTo(defaultConsoleLogger);
                syncLogForwarder.ConnectTo(defaultFileLogger);
            }

            public IServiceContextData Copy()
            {
                ContextData newContext = new ContextData();
                return newContext;
            }
        }

        private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static IMessageSource<LogMessage> QueuedLogSource => context.Data.queueLogForwarder;
        public static IMessageSource<LogMessage> SyncLogSource => context.Data.syncLogForwarder;
        public static IMessageSink<LogMessage> LogSink => context.Data.logSink;

        public static void SendLogMessage(in LogMessage msg)
        {
            context.Data.logSink.Post(in msg);
        }

        public static void FORCE(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.FORCE, shortText, detailText, contextHandle, caller, sourceFile, sourceLine));
        }

        public static void ERROR(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.ERROR, shortText, detailText, contextHandle, caller, sourceFile, sourceLine));
        }

        public static void WARNING(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.WARNING, shortText, detailText, contextHandle, caller, sourceFile, sourceLine));
        }

        public static void INFO(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.INFO, shortText, detailText, contextHandle, caller, sourceFile, sourceLine));
        }

        public static void DEBUG(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.DEBUG, shortText, detailText, contextHandle, caller, sourceFile, sourceLine));
        }

        public static void TRACE(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.TRACE, shortText, detailText, contextHandle, caller, sourceFile, sourceLine));
        }

        public static void ALWAYS(string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.FORCE, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public static void ERROR(string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.ERROR, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public static void WARNING(string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.WARNING, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public static void INFO(string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.INFO, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public static void DEBUG(string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.DEBUG, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public static void TRACE(string shortText,
                                 string detailText = "",
                                 [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                                 [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                                 [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.TRACE, shortText, detailText, default, caller, sourceFile, sourceLine));
        }
    }
}