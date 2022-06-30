using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using FeatureLoom.Workflows;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Logging
{
    public class FileLogger : Workflow<FileLogger.StateMachine>, IMessageSink
    {
        public class StateMachine : StateMachine<FileLogger>
        {
            protected override void Init()
            {
                var starting = State("Starting");
                var logging = State("Logging");

                starting.Build()
                    .Step("Load Configuration")
                        .Do(async c => await c.UpdateConfigAsync())
                    .Step("If NewFileOnStartup is configured force archiving.")
                        .If(c => c.config.newFileOnStartup)
                            .Do(c => c.ArchiveCurrentLogfile())
                    .Step("Start logging")
                        .Goto(logging);

                logging.Build("Logging")
                    .Step("Check for config update")
                        .Do(async c => await c.UpdateConfigAsync())
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
                            .WaitFor(c => c.delayBypass, c => c.config.delayAfterWritingInMs.Milliseconds())
                    .Step("Loop logging state")
                        .Loop();
            }
        }

        public class Config : Configuration
        {
            public string logFilePath = "logs/log.txt";
            public bool newFileOnStartup = true;

            public string archiveFilePath = "logs/logArchive.zip";
            public int logFilesArchiveLimitInMB = 100;
            public float logFileSizeLimitInMB = 5;
            public CompressionLevel compressionLevel = CompressionLevel.Fastest;
            public int delayAfterWritingInMs = 1000;
            public Loglevel skipDelayLogLevel = Loglevel.ERROR;
            public Loglevel logFileLoglevel = Loglevel.TRACE;
            public string logFileLogFormat = "";
            public int maxQueueSize = 10000;
        }

        public Config config;

        private QueueReceiver<LogMessage> receiver = new QueueReceiver<LogMessage>();
        private StringBuilder stringBuilder = new StringBuilder();
        private AsyncManualResetEvent delayBypass = new AsyncManualResetEvent(false);

        public void Post<M>(in M message)
        {
            if (message is LogMessage logMessage)
            {
                ((IMessageSink)receiver).Post(in logMessage);
                if (logMessage.level <= config.skipDelayLogLevel) delayBypass.Set();
            }
        }

        public void Post<M>(M message)
        {
            Post(in message);
        }

        public Task PostAsync<M>(M message)
        {
            Post(in message);
            return Task.CompletedTask;
        }

        private static readonly Comparer<ZipArchiveEntry> nameComparer = Comparer<ZipArchiveEntry>.Create((f1, f2) => f2.Name.CompareTo(f1.Name));

        public FileLogger(Config config = null)
        {
            this.config = config ?? new Config();
            this.config.TryUpdateFromStorage(false);
        }

        private async Task UpdateConfigAsync()
        {
            await config.TryUpdateFromStorageAsync(true);
            receiver.maxQueueSize = config.maxQueueSize;
        }

        private float GetLogFileSize()
        {
            var logFileInfo = new FileInfo(config.logFilePath);
            return logFileInfo.Exists ? logFileInfo.Length : 0;
        }

        private async Task WriteToLogFileAsync()
        {
            if (receiver.IsEmpty) return;

            
            var logFileInfo = new FileInfo(config.logFilePath);
            logFileInfo.Directory.Create();

            bool updateCreationTime = !logFileInfo.Exists;

            using (FileStream stream = File.Open(logFileInfo.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                if (updateCreationTime) logFileInfo.CreationTime = AppTime.CoarseNow;
                if (receiver.IsFull) await writer.WriteLineAsync(new LogMessage(Loglevel.WARNING, "LOGGING QUEUE OVERFLOW: Some log messages might be lost!").PrintToStringBuilder(stringBuilder, config.logFileLogFormat).GetStringAndClear());

                while (!receiver.IsEmpty)
                {
                    var messages = receiver.ReceiveAll();
                    foreach (var logMsg in messages)
                    {
                        if (logMsg.level <= config.logFileLoglevel)
                        {
                            await writer.WriteLineAsync(logMsg.PrintToStringBuilder(stringBuilder, config.logFileLogFormat).GetStringAndClear());
                        }
                    }
                }
            }

            delayBypass.Reset();
        }

        // TODO: Make async
        private void ArchiveCurrentLogfile()
        {
            var logFileInfo = new FileInfo(config.logFilePath);
            logFileInfo.Directory.Create();
            var archiveInfo = new FileInfo(config.archiveFilePath);
            archiveInfo.Directory.Create();

            logFileInfo.Refresh();
            if (!logFileInfo.Exists) return;

            using (var fileStream = new FileStream(archiveInfo.FullName, FileMode.OpenOrCreate))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, true))
                {
                    archive.CreateEntryFromFile(logFileInfo.FullName, logFileInfo.CreationTime.ToString("yyyy-MM-dd_HH-mm-ss") + " until " + logFileInfo.LastWriteTime.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt", config.compressionLevel);
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
    }
}