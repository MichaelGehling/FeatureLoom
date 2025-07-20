using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.MetaDatas;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System;

namespace FeatureLoom.Logging
{
    public class FileLogger : IMessageSink
    {

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

        private QueueReceiver<LogMessage> innerReceiver = null;
        private ReceiverBuffer<LogMessage> receiver = null;
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
            config = config ?? new Config();
            this.config = config;
            innerReceiver = new QueueReceiver<LogMessage>(config.maxQueueSize);
            receiver = new ReceiverBuffer<LogMessage>(innerReceiver);
            Task.Run(Run);
        }

        async Task Run()
        {
            await Task.Yield();
            this.config.TryUpdateFromStorage(false);

            var logHandle = this.GetType().Name + this.GetHandle().ToString();

            await UpdateConfigAsync().ConfiguredAwait();
            if (config.newFileOnStartup) ArchiveCurrentLogfile();
            while(true)
            {
                await UpdateConfigAsync().ConfiguredAwait();
                await receiver.WaitHandle.WaitAsync().ConfiguredAwait();
                try
                {
                    await WriteToLogFileAsync().ConfiguredAwait();
                }
                catch(Exception e)
                {
                    OptLog.ERROR(logHandle)?.Build($"Writing to log file failed.", e);
                }
                try
                {
                    if (config.logFileSizeLimitInMB * 1024 * 1024 <= GetLogFileSize()) ArchiveCurrentLogfile();
                }
                catch(Exception e)
                {
                    OptLog.ERROR(logHandle)?.Build($"Moving log file to archive failed.", e);
                }
                if (config.delayAfterWritingInMs > 0)
                {
                    await delayBypass.WaitAsync(config.delayAfterWritingInMs.Milliseconds()).ConfiguredAwait();
                }
            }
        }

        private async Task UpdateConfigAsync()
        {
            await config.TryUpdateFromStorageAsync(true).ConfiguredAwait();
            innerReceiver.maxQueueSize = config.maxQueueSize;
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
                if (receiver.IsFull) await writer.WriteLineAsync(new LogMessage(Loglevel.WARNING, "LOGGING QUEUE OVERFLOW: Some log messages might be lost!").PrintToStringBuilder(stringBuilder, config.logFileLogFormat).GetStringAndClear()).ConfiguredAwait();

                while (receiver.TryReceive(out var logMsg))
                {
                    if (logMsg.level <= config.logFileLoglevel)
                    {
                        await writer.WriteLineAsync(logMsg.PrintToStringBuilder(stringBuilder, config.logFileLogFormat).GetStringAndClear()).ConfiguredAwait();
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