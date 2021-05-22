using FeatureLoom.DataFlows;
using FeatureLoom.Extensions;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Logging
{
    public class DefaultFileLogger : Workflow<DefaultFileLogger.StateMachine>, IDataFlowSink
    {
        public class StateMachine : StateMachine<DefaultFileLogger>
        {
            protected override void Init()
            {
                var starting = State("Starting");
                var logging = State("Logging");

                starting.Build()
                    .Step("Load Configuration")
                        .Do(async c => await c.config.TryUpdateFromStorageAsync(true))
                    .Step("If NewFileOnStartup is configured force archiving.")
                        .If(c => c.config.newFileOnStartup)
                            .Do(c => c.ArchiveCurrentLogfile())
                    .Step("Start logging")
                        .Goto(logging);

                logging.Build("Logging")
                    .Step("Check for config update")
                        .Do(async c => await c.config.TryUpdateFromStorageAsync(true))
                    .Step("Wait until receiving logMessages")
                        .WaitFor(c => c.receiver)
                    .Step("Write all logMessages from receiver to file")
                        .Do(async c => await c.WriteToLogFileAsync())
                        .CatchAndDo((c, e) => Log.ERROR(c.GetHandle(), $"{c.Name}: Writing to log file failed.", e.ToString()))
                    .Step("Do archiving if file limit is exceeded")
                        .If(c => c.config.logFileSizeLimitInMB * 1024 * 1024 <= c.GetLogFileSize())
                            .Do(c => c.ArchiveCurrentLogfile())
                        .CatchAndDo((c, e) => Log.ERROR(c.GetHandle(), $"{c.Name}: Moving log file to archive failed.", e.ToString()))
                    .Step("Delay before next writing, if configured")
                        .If(c => c.config.delayAfterWritingInMs > 0)
                            .Wait(c => c.config.delayAfterWritingInMs.Milliseconds())
                    .Step("Loop logging state")
                        .Loop();
            }
        }

        public class Config : Configuration
        {
            public string logFilePath = "log.txt";
            public bool newFileOnStartup = true;

            public string archiveFilePath = "logArchive.zip";
            public int logFilesArchiveLimitInMB = 100;
            public float logFileSizeLimitInMB = 5;
            public CompressionLevel compressionLevel = CompressionLevel.Fastest;
            public int delayAfterWritingInMs = 0;
            public Loglevel logFileLoglevel = Loglevel.TRACE;
            public string logFileLogFormat = "";
            public int bufferingQueueSize = 10000;
        }

        public Config config;

        private QueueReceiver<LogMessage> receiver;
        private string logFilePath;
        private string archiveFilePath;
        private StringBuilder stringBuilder = new StringBuilder();

        public void Post<M>(in M message)
        {
            ((IDataFlowSink)receiver).Post(in message);
        }

        public void Post<M>(M message)
        {
            ((IDataFlowSink)receiver).Post(message);
        }

        private static readonly Comparer<ZipArchiveEntry> nameComparer = Comparer<ZipArchiveEntry>.Create((f1, f2) => f2.Name.CompareTo(f1.Name));

        public DefaultFileLogger(Config config = null)
        {
            this.config = config ?? new Config();
            this.config.TryUpdateFromStorage(true);
            this.logFilePath = new FileInfo(this.config.logFilePath).FullName;
            this.archiveFilePath = new FileInfo(this.config.archiveFilePath).FullName;
            receiver = new QueueReceiver<LogMessage>(config.bufferingQueueSize);
        }

        private float GetLogFileSize()
        {
            var logFileInfo = new FileInfo(logFilePath);
            return logFileInfo.Length;
        }

        private async Task WriteToLogFileAsync()
        {
            if (receiver.IsEmpty) return;

            bool updateCreationTime = false;
            var logFileInfo = new FileInfo(logFilePath);
            if (!logFileInfo.Exists) updateCreationTime = true;

            using (FileStream stream = File.Open(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                if (updateCreationTime) logFileInfo.CreationTime = AppTime.Now;
                if (receiver.IsFull) await writer.WriteLineAsync(new LogMessage(Loglevel.WARNING, "LOGGING QUEUE OVERFLOW: Some log messages might be lost!").PrintToStringBuilder(stringBuilder, config.logFileLogFormat).GetStringAndClear());

                var messages = receiver.ReceiveAll();
                foreach (var msg in messages)
                {
                    if (msg is LogMessage logMsg)
                    {
                        if (logMsg.level <= config.logFileLoglevel)
                        {
                            await writer.WriteLineAsync(logMsg.PrintToStringBuilder(stringBuilder, config.logFileLogFormat).GetStringAndClear());
                        }
                    }
                    else await writer.WriteLineAsync(msg.ToString());
                }
            }
        }

        // TODO: Make async
        private void ArchiveCurrentLogfile()
        {
            var logFileInfo = new FileInfo(logFilePath);
            logFileInfo.Refresh();
            if (!logFileInfo.Exists) return;

            using (var fileStream = new FileStream(archiveFilePath, FileMode.OpenOrCreate))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, true))
                {
                    archive.CreateEntryFromFile(logFilePath, logFileInfo.CreationTime.ToString("yyyy-MM-dd_HH-mm-ss") + " until " + logFileInfo.LastWriteTime.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt", config.compressionLevel);
                }

                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, true))
                {
                    List<ZipArchiveEntry> entries = new List<ZipArchiveEntry>(archive.Entries);
                    entries.Sort(nameComparer);
                    long overallLength = 0;
                    foreach (var entry in entries)
                    {
                        if (overallLength > config.logFilesArchiveLimitInMB * 1024 * 1024)
                        {
                            entry.Delete();
                        }
                        else overallLength += entry.CompressedLength;
                    }
                }
            }
            logFileInfo.Delete();
        }

        public Task PostAsync<M>(M message)
        {
            return ((IDataFlowSink)receiver).PostAsync(message);
        }
    }
}