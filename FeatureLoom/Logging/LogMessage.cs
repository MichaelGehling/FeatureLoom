using FeatureLoom.MetaDatas;
using FeatureLoom.Time;
using System;
using System.Text;
using System.Threading;

namespace FeatureLoom.Logging
{
    public readonly struct LogMessage
    {
        public LogMessage(Loglevel level,
                          string shortText,
                          string detailText = "",
                          ObjectHandle contextHandle = default,
                          [System.Runtime.CompilerServices.CallerMemberName] string caller = "",
                          [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "",
                          [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
        {
            this.shortText = shortText;
            this.level = level;
            this.timeStamp = AppTime.Now;
            if (detailText != null && detailText != "") detailText = $"\n  {detailText}";
            this.detailText = detailText;
            this.caller = caller;
            this.sourceFile = sourceFile;
            this.sourceLine = sourceLine;
            this.threadId = Thread.CurrentThread.ManagedThreadId;
            this.contextHandle = contextHandle;
        }

        public readonly DateTime timeStamp;
        public readonly string shortText;
        public readonly string detailText;
        public readonly Loglevel level;
        public readonly ObjectHandle contextHandle;
        public readonly string caller;
        public readonly string sourceFile;
        public readonly int sourceLine;
        public readonly int threadId;

        public static string defaultFormat = "| {0} | ctxt{4} | thrd{3} | {1} | {2} | File: {5} Line: {6} Method: {7} | {8} |";
        public static string defaultTimeStampFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

        public string Print(string format = null, string timeStampFormat = null)
        {
            return PrintToStringBuilder().ToString();
        }

        public StringBuilder PrintToStringBuilder(StringBuilder sb = null, string format = null, string timeStampFormat = null)
        {
            if (format == null || format == "") format = defaultFormat;
            if (timeStampFormat == null || timeStampFormat == "") timeStampFormat = defaultTimeStampFormat;
            if (sb == null) sb = new StringBuilder();

            return sb.AppendFormat(format,
                timeStamp.ToString(timeStampFormat),
                level,
                shortText,
                threadId,
                contextHandle,
                sourceFile,
                sourceLine,
                caller,
                detailText);
        }

        public override string ToString()
        {
            return Print();
        }
    }
}