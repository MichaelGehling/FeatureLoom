using FeatureFlowFramework.Aspects;
using FeatureFlowFramework.DataFlows;
using System;

namespace FeatureFlowFramework.Logging
{
    public static class Log
    {
        static Log()
        {
            logSender.ConnectTo(logForwarder);
            logForwarder.ConnectTo(defaultConsoleLogger);
            logForwarder.ConnectTo(defaultFileLogger);
            defaultFileLogger.Run();
        }

        private static readonly Sender<LogMessage> logSender = new Sender<LogMessage>();
        public static readonly ActiveForwarder logForwarder = new ActiveForwarder(1, 1000, 10, 10000, TimeSpan.Zero, true);
        public static DefaultConsoleLogger defaultConsoleLogger = new DefaultConsoleLogger();
        public static DefaultFileLogger defaultFileLogger = new DefaultFileLogger();

        public static void SendLogMessage(LogMessage msg)
        {
            logSender.Send(msg);
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