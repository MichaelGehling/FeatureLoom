using FeatureLoom.Collections;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace FeatureLoom.Logging;


public class OptLogService
{
    public class Settings : Configuration
    {
        public List<LogFilterSettings> whiteListFilterSettings = new List<LogFilterSettings>();
        public List<LogFilterSettings> blackListFilterSettings = new List<LogFilterSettings>();
        public Loglevel globalLogLevel = Loglevel.INFO;
        public List<Loglevel> logLevelsToAddStackTrace = new List<Loglevel> { Loglevel.ERROR, Loglevel.CRITICAL };
        public bool logOnWrongUsage = true;
    }
    internal Settings settings = new Settings();

    Pool<FilteredLogger> loggerPool;
    LazyValue<AsyncLocal<ObjectHandle>> roamingContext;
    LogFilter[] whiteListFilters_IMPORTANT = [];
    LogFilter[] blackListFilters_IMPORTANT = [];
    LogFilter[] whiteListFilters_CRITICAL = [];
    LogFilter[] blackListFilters_CRITICAL = [];
    LogFilter[] whiteListFilters_ERROR = [];
    LogFilter[] blackListFilters_ERROR = [];
    LogFilter[] whiteListFilters_WARNING = [];
    LogFilter[] blackListFilters_WARNING = [];
    LogFilter[] whiteListFilters_INFO = [];
    LogFilter[] blackListFilters_INFO = [];
    LogFilter[] whiteListFilters_DEBUG = [];
    LogFilter[] blackListFilters_DEBUG = [];
    LogFilter[] whiteListFilters_TRACE = [];
    LogFilter[] blackListFilters_TRACE = [];
    int globalLogLevel = Loglevel.INFO.ToInt();
    static int loglevel_IMPORTANT = Loglevel.IMPORTANT.ToInt();
    static int loglevel_CRITICAL = Loglevel.CRITICAL.ToInt();
    static int loglevel_ERROR = Loglevel.ERROR.ToInt();
    static int loglevel_WARNING = Loglevel.WARNING.ToInt();
    static int loglevel_INFO = Loglevel.INFO.ToInt();
    static int loglevel_DEBUG = Loglevel.DEBUG.ToInt();
    static int loglevel_TRACE = Loglevel.TRACE.ToInt();

    MicroLock filtersLock = new MicroLock();

    public OptLogService(Settings settings)
    {
        ApplySettings(settings);
        loggerPool = new(() => new FilteredLogger(this));
    }

    public OptLogService()
    {
        ApplySettings(null);
        loggerPool = new(() => new FilteredLogger(this));
    }

    internal void ReturnLoggerToPool(FilteredLogger logger) => loggerPool.Return(logger);

    public void ApplySettings(Settings settings)
    {
        if (settings == null) settings = new Settings();
        this.settings = settings;
        globalLogLevel = settings.globalLogLevel.ToInt();
        using (filtersLock.Lock())
        {
            var whiteListFilters = settings.whiteListFilterSettings.Select(filterSettings => new LogFilter(this, filterSettings));
            whiteListFilters_IMPORTANT = whiteListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.IMPORTANT)).ToArray();
            whiteListFilters_CRITICAL = whiteListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.CRITICAL)).ToArray();
            whiteListFilters_ERROR = whiteListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.ERROR)).ToArray();
            whiteListFilters_WARNING = whiteListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.WARNING)).ToArray();
            whiteListFilters_INFO = whiteListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.INFO)).ToArray();
            whiteListFilters_DEBUG = whiteListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.DEBUG)).ToArray();
            whiteListFilters_TRACE = whiteListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.TRACE)).ToArray();

            var blackListFilters = settings.blackListFilterSettings.Select(filterSettings => new LogFilter(this, filterSettings));
            blackListFilters_IMPORTANT = blackListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.IMPORTANT)).ToArray();
            blackListFilters_CRITICAL = blackListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.CRITICAL)).ToArray();
            blackListFilters_ERROR = blackListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.ERROR)).ToArray();
            blackListFilters_WARNING = blackListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.WARNING)).ToArray();
            blackListFilters_INFO = blackListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.INFO)).ToArray();
            blackListFilters_DEBUG = blackListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.DEBUG)).ToArray();
            blackListFilters_TRACE = blackListFilters.Where(filter => filter.MatchesLogLevel(Loglevel.TRACE)).ToArray();
        }
    }

    public ObjectHandle LogContext
    {
        get => roamingContext.Exists ? roamingContext.Obj.Value : ObjectHandle.Invalid;
        set
        {
            roamingContext.Obj.Value = value;
        }
    }

    public void AddWhiteListFilter(LogFilterSettings logFilterSettings)
    {
        LogFilter logFilter = new LogFilter(this, logFilterSettings);
        using (filtersLock.Lock())
        {
            if (logFilter.MatchesLogLevel(Loglevel.IMPORTANT)) whiteListFilters_IMPORTANT = whiteListFilters_IMPORTANT.AddToCopy(logFilter);
            if (logFilter.MatchesLogLevel(Loglevel.CRITICAL)) whiteListFilters_CRITICAL = whiteListFilters_CRITICAL.AddToCopy(logFilter);
            if (logFilter.MatchesLogLevel(Loglevel.ERROR)) whiteListFilters_ERROR = whiteListFilters_ERROR.AddToCopy(logFilter);
            if (logFilter.MatchesLogLevel(Loglevel.WARNING)) whiteListFilters_WARNING = whiteListFilters_WARNING.AddToCopy(logFilter);
            if (logFilter.MatchesLogLevel(Loglevel.INFO)) whiteListFilters_INFO = whiteListFilters_INFO.AddToCopy(logFilter);
            if (logFilter.MatchesLogLevel(Loglevel.DEBUG)) whiteListFilters_DEBUG = whiteListFilters_DEBUG.AddToCopy(logFilter);
            if (logFilter.MatchesLogLevel(Loglevel.TRACE)) whiteListFilters_TRACE = whiteListFilters_TRACE.AddToCopy(logFilter);
        }
    }


    public void AddBlackListFilter(LogFilterSettings logFilterSettings)
    {
        LogFilter logFilter = new LogFilter(this, logFilterSettings);
        using (filtersLock.Lock())
        {
            using (filtersLock.Lock())
            {
                if (logFilter.MatchesLogLevel(Loglevel.IMPORTANT)) blackListFilters_IMPORTANT = blackListFilters_IMPORTANT.AddToCopy(logFilter);
                if (logFilter.MatchesLogLevel(Loglevel.CRITICAL)) blackListFilters_CRITICAL = blackListFilters_CRITICAL.AddToCopy(logFilter);
                if (logFilter.MatchesLogLevel(Loglevel.ERROR)) blackListFilters_ERROR = blackListFilters_ERROR.AddToCopy(logFilter);
                if (logFilter.MatchesLogLevel(Loglevel.WARNING)) blackListFilters_WARNING = blackListFilters_WARNING.AddToCopy(logFilter);
                if (logFilter.MatchesLogLevel(Loglevel.INFO)) blackListFilters_INFO = blackListFilters_INFO.AddToCopy(logFilter);
                if (logFilter.MatchesLogLevel(Loglevel.DEBUG)) blackListFilters_DEBUG = blackListFilters_DEBUG.AddToCopy(logFilter);
                if (logFilter.MatchesLogLevel(Loglevel.TRACE)) blackListFilters_TRACE = blackListFilters_TRACE.AddToCopy(logFilter);
            }
        }
    }

    public class LogFilterSettings
    {
        public Loglevel? minloglevel;
        public Loglevel? maxloglevel;
        public string? sourceFileMask = null;
        public string? methodMask = null;
        public int? minLine;
        public int? maxLine;
        public ObjectHandle contextFilter;
    }

    class LogFilter
    {
        OptLogService parent;
        LogFilterSettings settings;

        public LogFilter(OptLogService parent, LogFilterSettings settings)
        {
            this.parent = parent;
            this.settings = settings;
        }

        public bool MatchesLogLevel(Loglevel loglevel)
        {
            if (settings.maxloglevel.HasValue && loglevel > settings.maxloglevel) return false;
            if (settings.minloglevel.HasValue && loglevel < settings.minloglevel) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Check(string method, string sourceFile, int sourceLine, ref ObjectHandle logContext)
        {
            if (settings.sourceFileMask != null && !sourceFile.MatchesWildcard(settings.sourceFileMask)) return false;
            if (settings.methodMask != null && !method.MatchesWildcard(settings.methodMask)) return false;
            if (settings.minLine.HasValue && settings.maxLine.HasValue && (sourceLine < settings.minLine || sourceLine > settings.maxLine)) return false;
            if (settings.contextFilter.IsValid)
            {
                if (logContext.IsValid)
                {
                    if (logContext != settings.contextFilter) return false;
                }
                else if (parent.roamingContext.Exists)
                {
                    var roamingContext = parent.roamingContext.Obj.Value;
                    if (roamingContext.IsValid)
                    {
                        logContext = roamingContext;
                        if (logContext != settings.contextFilter) return false;
                    }
                }
            }

            return true;
        }
    }

    public class FilteredLogger : IDisposable
    {
        private OptLogService parent;
        int logLevelValue;
        ObjectHandle logContext;
        string method;
        string sourceFile;
        int sourceLine;

        public FilteredLogger(OptLogService parent)
        {
            this.parent = parent;
        }

        public void Dispose()
        {
            logContext = default;
            parent.ReturnLoggerToPool(this);
        }

        internal void Prepare(int logLevelValue, ObjectHandle logContext, string method, string sourceFile, int sourceLine)
        {
            this.logLevelValue = logLevelValue;
            this.logContext = logContext;
            this.method = method;
            this.sourceFile = sourceFile;
            this.sourceLine = sourceLine;
        }

        internal void ActuallyBuild(string shortText, string detailText, bool addStackTrace)
        {
            EnumHelper.TryFromInt(logLevelValue, out Loglevel logLevel);
            if (!addStackTrace) addStackTrace = parent.settings.logLevelsToAddStackTrace.Contains(logLevel);
            if (addStackTrace) detailText = $"{(detailText.EmptyOrNull() ? "" : detailText + "\n")}{Environment.StackTrace.ReplaceBetween(null, Environment.NewLine, "", true).ReplaceBetween(null, Environment.NewLine, "", true)}";
            var logMessage = new LogMessage(logLevel, shortText, detailText, logContext, method, sourceFile, sourceLine);
            Service<LogService>.Instance.SendLogMessage(in logMessage);
        }

        internal void SendLogMessage(in LogMessage logMessage)
        {
            Service<LogService>.Instance.SendLogMessage(in logMessage);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger IMPORTANT(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_IMPORTANT, whiteListFilters_IMPORTANT, loglevel_IMPORTANT, logContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger CRITICAL(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_CRITICAL, whiteListFilters_CRITICAL, loglevel_CRITICAL, logContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger ERROR(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_ERROR, whiteListFilters_ERROR, loglevel_ERROR, logContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger WARNING(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_WARNING, whiteListFilters_WARNING, loglevel_WARNING, logContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger INFO(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_INFO, whiteListFilters_INFO, loglevel_INFO, logContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger DEBUG(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_DEBUG, whiteListFilters_DEBUG, loglevel_DEBUG, logContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger TRACE(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_TRACE, whiteListFilters_TRACE, loglevel_TRACE, logContext);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredLogger WithLevel(Loglevel loglevel, ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1)
    {
        switch (loglevel)
        {
            case Loglevel.TRACE: return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_TRACE, whiteListFilters_TRACE, loglevel_TRACE, logContext); break;
            case Loglevel.DEBUG: return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_DEBUG, whiteListFilters_DEBUG, loglevel_DEBUG, logContext); break;
            case Loglevel.INFO: return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_INFO, whiteListFilters_INFO, loglevel_INFO, logContext); break;
            case Loglevel.WARNING: return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_WARNING, whiteListFilters_WARNING, loglevel_WARNING, logContext); break;
            case Loglevel.ERROR: return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_ERROR, whiteListFilters_ERROR, loglevel_ERROR, logContext); break;
            case Loglevel.CRITICAL: return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_CRITICAL, whiteListFilters_CRITICAL, loglevel_CRITICAL, logContext); break;
            case Loglevel.IMPORTANT: return LoggerIfPassed(method, sourceFile, sourceLine, blackListFilters_IMPORTANT, whiteListFilters_IMPORTANT, loglevel_IMPORTANT, logContext); break;
            default: return null;
        }        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FilteredLogger LoggerIfPassed(string method, string sourceFile, int sourceLine, LogFilter[] blackListFilters, LogFilter[] whiteListFilters, int logLevel, ObjectHandle logContext)
    {
        if (globalLogLevel >= logLevel)
        {
            for (int i = 0; i < blackListFilters.Length; i++)
            {
                if (blackListFilters[i].Check(method, sourceFile, sourceLine, ref logContext)) return null;
            }
            return PrepareLogger(logLevel, logContext, method, sourceFile, sourceLine);
        }
        else
        {
            for (int i = 0; i < whiteListFilters.Length; i++)
            {
                if (whiteListFilters[i].Check(method, sourceFile, sourceLine, ref logContext)) return PrepareLogger(logLevel, logContext, method, sourceFile, sourceLine);
            }
            return null;
        }
    }    

    private FilteredLogger PrepareLogger(int logLevel, ObjectHandle logContext, string method, string sourceFile, int sourceLine)
    {
        if (!logContext.IsValid && roamingContext.Exists)
        {
            var temp = roamingContext.Obj.Value;
            if (temp.IsValid) logContext = temp;
        }
        var logger = loggerPool.Take();
        logger.Prepare(logLevel, logContext, method, sourceFile, sourceLine);
        return logger;
    }
}

public static class FilteredLoggerExtension
{
    public static void Build(this OptLogService.FilteredLogger? logger, string shortText, string detailText = null, bool addStackTrace = false)
    {
        if (logger == null)
        {
            if (Service<OptLogService>.Instance.settings.logOnWrongUsage) Log.WARNING("OptLog is not used correctly. To improve performance use it with a null check, e.g. OptLog.ERROR()?.Build(\"My log message\")", "", true);
            return;
        }
        else logger.ActuallyBuild(shortText, detailText, addStackTrace);
    }

    public static void Send(this OptLogService.FilteredLogger? logger, in LogMessage preparedLogMessage)
    {
        if (logger == null)
        {
            if (Service<OptLogService>.Instance.settings.logOnWrongUsage) Log.WARNING("OptLog is not used correctly. To improve performance use it with a null check, e.g. OptLog.ERROR()?.Send(MyPreparedMessage)", "", true);
            return;
        }
        else logger.SendLogMessage(preparedLogMessage);
    }
}


     
