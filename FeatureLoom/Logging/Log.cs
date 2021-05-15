using FeatureLoom.DataFlows;
using FeatureLoom.Helpers;
using FeatureLoom.MetaDatas;
using FeatureLoom.Workflows;
using System;

namespace FeatureLoom.Logging
{
    public static class Log
    {
        public static DefaultConsoleLogger defaultConsoleLogger = new DefaultConsoleLogger();
        public static DefaultFileLogger defaultFileLogger = new DefaultFileLogger();
        public static IWorkflowRunner logRunner = new SuspendingAsyncRunner();

        static Log()
        {
            WorkflowRunnerService.Unregister(logRunner);
            logRunner.Run(defaultFileLogger);
        }

        private class ContextData : IServiceContextData
        {
            public readonly Sender<LogMessage> logSender = new Sender<LogMessage>();
            public readonly QueueForwarder<LogMessage> logForwarder = new QueueForwarder<LogMessage>(1, 1000, 10, 10000, TimeSpan.Zero, true);
            public readonly Forwarder<LogMessage> syncForwarder = new Forwarder<LogMessage>();

            public ContextData()
            {
                logSender.ConnectTo(logForwarder);
                logSender.ConnectTo(syncForwarder);
                logForwarder.ConnectTo(defaultConsoleLogger);
                logForwarder.ConnectTo(defaultFileLogger);
            }

            public IServiceContextData Copy()
            {
                ContextData newContext = new ContextData();
                return newContext;
            }
        }

        private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static IDataFlowConnection<LogMessage> LogForwarder => context.Data.logForwarder;
        public static IDataFlowConnection<LogMessage> SyncLogForwarder => context.Data.syncForwarder;

        public static void SendLogMessage(LogMessage msg)
        {
            context.Data.logSender.Send(msg);
        }

        public static void ALWAYS(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.ALWAYS, shortText, detailText, contextHandle, caller, sourceFile, sourceLine));
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
            SendLogMessage(new LogMessage(Loglevel.ALWAYS, shortText, detailText, default, caller, sourceFile, sourceLine));
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