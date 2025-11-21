using FeatureLoom.MessageFlow;
using FeatureLoom.MetaDatas;
using System;
using FeatureLoom.Storages;
using FeatureLoom.Extensions;
using FeatureLoom.DependencyInversion;

namespace FeatureLoom.Logging
{
    public class LogService
    {
        public ConsoleLogger DefaultConsoleLogger { get; set; }
        public FileLogger DefaultFileLogger { get; set; }

        readonly Forwarder<LogMessage> logSink = new Forwarder<LogMessage>();
        readonly QueueForwarder queueLogForwarder = new QueueForwarder(1, 1000, 10, 10000, TimeSpan.Zero, true);
        readonly Forwarder<LogMessage> syncLogForwarder = new Forwarder<LogMessage>();
        readonly Config settings = new Config();

        public LogService()
        {
            DefaultConsoleLogger = new ConsoleLogger();
            DefaultFileLogger = new FileLogger();

            logSink.ConnectTo(queueLogForwarder);
            logSink.ConnectTo(syncLogForwarder);
            queueLogForwarder.ConnectTo(DefaultConsoleLogger);
            syncLogForwarder.ConnectTo(DefaultFileLogger);
        }

        public IMessageSource QueuedLogSource => queueLogForwarder;
        public IMessageSource<LogMessage> SyncLogSource => syncLogForwarder;
        public IMessageSink<LogMessage> LogSink => logSink;
        public Config Settings => settings;

        public void SendLogMessage(in LogMessage msg)
        {
            logSink.Post(in msg);
        }

        public void FORCE(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.IMPORTANT, shortText, detailText, contextHandle.ToString(), caller, sourceFile, sourceLine));
        }

        public void ERROR(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace || settings.addStackTraceToErrors) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.ERROR, shortText, detailText, contextHandle.ToString(), caller, sourceFile, sourceLine));
        }

        public void WARNING(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.WARNING, shortText, detailText, contextHandle.ToString(), caller, sourceFile, sourceLine));
        }

        public void INFO(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.INFO, shortText, detailText, contextHandle.ToString(), caller, sourceFile, sourceLine));
        }

        public void DEBUG(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.DEBUG, shortText, detailText, contextHandle.ToString(), caller, sourceFile, sourceLine));
        }

        public void TRACE(ObjectHandle contextHandle,
                          string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.TRACE, shortText, detailText, contextHandle.ToString(), caller, sourceFile, sourceLine));
        }

        public void FORCE(string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.IMPORTANT, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public void ERROR(string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace || settings.addStackTraceToErrors) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.ERROR, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public void WARNING(string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.WARNING, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public void INFO(string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.INFO, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public void DEBUG(string shortText,
                          string detailText = "",
                          bool addStackTrace = false,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.DEBUG, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public void TRACE(string shortText,
                                 string detailText = "",
                                 bool addStackTrace = false,
                                 [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                                 [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                                 [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            SendLogMessage(new LogMessage(Loglevel.TRACE, shortText, detailText, default, caller, sourceFile, sourceLine));
        }

        public void LogOnException(string actionDescription, Action action, bool logActionStart = false, bool logActionFinished = false, bool rethrowException = true, Loglevel exceptionLogLevel = Loglevel.ERROR)
        {
            if (logActionStart) INFO($"Starting: {actionDescription}.");
            try
            {
                action();
            }
            catch (Exception e)
            {
                actionDescription = actionDescription ?? "Action";
                ERROR($"Exception occured while: {actionDescription}! {(rethrowException ? "" : "(Process will continue.)")}", e.ToString());
                if (rethrowException) throw;
            }

            if (logActionFinished) INFO($"Finished: {actionDescription}.");
        }

        public class Config : Configuration
        {
            public bool addStackTraceToErrors = false;
        }
    }
}