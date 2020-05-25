using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Services.MetaData;
using System;
using System.Text;
using System.Threading;

namespace FeatureFlowFramework.Services.Logging
{
    public struct LogMessage
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
            if(detailText != null && detailText != "") detailText = $"\n  {detailText}";
            this.detailText = detailText;
            this.caller = caller;
            this.sourceFile = sourceFile;
            this.sourceLine = sourceLine;
            this.threadId = Thread.CurrentThread.ManagedThreadId;
            this.contextHandle = contextHandle;
        }

        public DateTime timeStamp;
        public string shortText;
        public string detailText;
        public Loglevel level;
        public ObjectHandle contextHandle;
        public string caller;
        public string sourceFile;
        public int sourceLine;
        public int threadId;

        public static string defaultFormat = "| {0} | {4} | {1} | {2} | {3} | File: {5} Line: {6} Method: {7} | {8} |";
        public static string defaultTimeStampFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

        public string Print(string format = null, string timeStampFormat = null)
        {
            return PrintToStringBuilder().ToString();
        }

        public StringBuilder PrintToStringBuilder(StringBuilder sb = null, string format = null, string timeStampFormat = null)
        {
            if(format == null || format == "") format = defaultFormat;
            if(timeStampFormat == null || timeStampFormat == "") timeStampFormat = defaultTimeStampFormat;
            if(sb == null) sb = new StringBuilder();

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