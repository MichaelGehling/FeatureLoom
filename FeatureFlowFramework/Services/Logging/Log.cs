using FeatureFlowFramework.Aspects;
using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Workflows;
using System;

namespace FeatureFlowFramework.Services.Logging
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

        class Context : IServiceContextData
        {
            public readonly Sender<LogMessage> logSender = new Sender<LogMessage>();
            public readonly ActiveForwarder logForwarder = new ActiveForwarder(1, 1000, 10, 10000, TimeSpan.Zero, true);
            public readonly BufferingForwarder<LogMessage> logForwarderBuffer = new BufferingForwarder<LogMessage>(1000);            

            public Context()
            {
                logSender.ConnectTo(logForwarder);
                logForwarder.ConnectTo(logForwarderBuffer);
                logForwarder.ConnectTo(defaultConsoleLogger);
                logForwarder.ConnectTo(defaultFileLogger);
            }

            public IServiceContextData Copy()
            {
                Context newContext = new Context();
                newContext.logForwarderBuffer.AddRangeToBuffer(logForwarderBuffer.GetAllBufferEntries());
                return newContext;
            }
        }
        static ServiceContext<Context> context = new ServiceContext<Context>();

        public static BufferingForwarder<LogMessage> LogForwarderBuffer => context.Data.logForwarderBuffer;
        public static ActiveForwarder LogForwarder => context.Data.logForwarder;

        public static void SendLogMessage(LogMessage msg)
        {
            context.Data.logSender.Send(msg);
        }

        public static bool IsEnabledFor(Loglevel level, string contextName = null)
        {
            // TODO
            return true;
        }

        public static void ALWAYS(object context,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.ALWAYS, shortText, detailText, context.GetAspectHandle(), caller, sourceFile, sourceLine));
        }

        public static void ERROR(object context,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.ERROR, shortText, detailText, context.GetAspectHandle(), caller, sourceFile, sourceLine));
        }

        public static void WARNING(object context,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.WARNING, shortText, detailText, context.GetAspectHandle(), caller, sourceFile, sourceLine));
        }

        public static void INFO(object context,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.INFO, shortText, detailText, context.GetAspectHandle(), caller, sourceFile, sourceLine));
        }

        public static void DEBUG(object context,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.DEBUG, shortText, detailText, context.GetAspectHandle(), caller, sourceFile, sourceLine));
        }

        public static void TRACE(object context,
                          string shortText,
                          string detailText = "",
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            SendLogMessage(new LogMessage(Loglevel.TRACE, shortText, detailText, context.GetAspectHandle(), caller, sourceFile, sourceLine));
        }
    }
}