using FeatureLoom.DependencyInversion;
using FeatureLoom.MetaDatas;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Logging;

public static class OptLog
{
    public static ObjectHandle LogContext { get => Service<OptLogService>.Instance.LogContext; set => Service<OptLogService>.Instance.LogContext = value; }

    public static void AddBlackListFilter(OptLogService.LogFilterSettings logFilterSettings) => Service<OptLogService>.Instance.AddBlackListFilter(logFilterSettings);
    public static void AddWhiteListFilter(OptLogService.LogFilterSettings logFilterSettings) => Service<OptLogService>.Instance.AddWhiteListFilter(logFilterSettings);
    public static void ApplySettings(OptLogService.Settings settings) => Service<OptLogService>.Instance.ApplySettings(settings);
    public static OptLogService.FilteredLogger CRITICAL(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.CRITICAL(logContext, method, sourceFile, sourceLine);
    public static OptLogService.FilteredLogger DEBUG(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.DEBUG(logContext, method, sourceFile, sourceLine);
    public static OptLogService.FilteredLogger ERROR(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.ERROR(logContext, method, sourceFile, sourceLine);
    public static OptLogService.FilteredLogger IMPORTANT(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.IMPORTANT(logContext, method, sourceFile, sourceLine);
    public static OptLogService.FilteredLogger INFO(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.INFO(logContext, method, sourceFile, sourceLine);
    public static OptLogService.FilteredLogger TRACE(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.TRACE(logContext, method, sourceFile, sourceLine);
    public static OptLogService.FilteredLogger WARNING(ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.WARNING(logContext, method, sourceFile, sourceLine);
    public static OptLogService.FilteredLogger WithLevel(Loglevel loglevel, ObjectHandle logContext = default, [CallerMemberName] string method = null, [CallerFilePath] string sourceFile = null, [CallerLineNumber] int sourceLine = -1) => Service<OptLogService>.Instance.WithLevel(loglevel, logContext, method, sourceFile, sourceLine);
}