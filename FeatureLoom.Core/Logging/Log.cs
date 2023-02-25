using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.MetaDatas;
using FeatureLoom.Workflows;
using System;
using FeatureLoom.Storages;
using FeatureLoom.Extensions;
using System.Runtime.CompilerServices;
using FeatureLoom.DependencyInversion;

namespace FeatureLoom.Logging
{
    public static class Log
    {
        public static ConsoleLogger DefaultConsoleLogger { get => Service<LogService>.Instance.DefaultConsoleLogger; set => Service<LogService>.Instance.DefaultConsoleLogger = value; }
        public static FileLogger DefaultFileLogger { get => Service<LogService>.Instance.DefaultFileLogger; set => Service<LogService>.Instance.DefaultFileLogger = value; }

        public static IMessageSink<LogMessage> LogSink => Service<LogService>.Instance.LogSink;

        public static IMessageSource<LogMessage> QueuedLogSource => Service<LogService>.Instance.QueuedLogSource;

        public static LogService.Config Settings => Service<LogService>.Instance.Settings;

        public static IMessageSource<LogMessage> SyncLogSource => Service<LogService>.Instance.SyncLogSource;

        public static void DEBUG(ObjectHandle contextHandle, string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.DEBUG(contextHandle, shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void DEBUG(string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.DEBUG(shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void ERROR(ObjectHandle contextHandle, string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.ERROR(contextHandle, shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void ERROR(string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.ERROR(shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void FORCE(ObjectHandle contextHandle, string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.FORCE(contextHandle, shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void FORCE(string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.FORCE(shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void INFO(ObjectHandle contextHandle, string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.INFO(contextHandle, shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void INFO(string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.INFO(shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void LogOnException(string actionDescription, Action action, bool logActionStart = false, bool logActionFinished = false, bool rethrowException = true, Loglevel exceptionLogLevel = Loglevel.ERROR)
        {
            Service<LogService>.Instance.LogOnException(actionDescription, action, logActionStart, logActionFinished, rethrowException, exceptionLogLevel);
        }

        public static void SendLogMessage(in LogMessage msg)
        {
            Service<LogService>.Instance.SendLogMessage(in msg);
        }

        public static void TRACE(ObjectHandle contextHandle, string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.TRACE(contextHandle, shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void TRACE(string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.TRACE(shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void WARNING(ObjectHandle contextHandle, string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.WARNING(contextHandle, shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }

        public static void WARNING(string shortText, string detailText = "", bool addStackTrace = false, [CallerMemberName] string caller = "", [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLine = 0)
        {
            Service<LogService>.Instance.WARNING(shortText, detailText, addStackTrace, caller, sourceFile, sourceLine);
        }
    }
}