using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
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
using System.Threading;
using FeatureLoom.Helpers;

namespace FeatureLoom.Logging
{
    public class FileLogger : IMessageSink
    {

        public class Config : Configuration
        {
            public string logFilePath = "logs/log.txt";
            public bool newFileOnStartup = true;
            public bool compressedArchive = true;
            public float logFileSizeLimitInMB = 5;
            public string archiveFilePath = "logs/logArchive.zip";
            public int logFilesArchiveLimitInMB = 100;            
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
        private CancellationTokenSource cts = new CancellationTokenSource();

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
            Start();
            this.AttachDestructor(_ => this.Stop());
        }

        public void Stop() => cts.Cancel();
        public void Start()
        {            
            cts = new CancellationTokenSource();
            Task.Run(Run);
        }

        async Task Run()
        {            
            await AppTime.WaitAsync(200.Milliseconds()).ConfiguredAwait(); // wait a bit to let the system start up and avoid file access issues
            if (cts.IsCancellationRequested) return;

            this.config.TryUpdateFromStorage(false);
            var logHandle = this.GetType().Name + this.GetHandle().ToString();

            try
            {
                await UpdateConfigAsync().ConfiguredAwait();
                if (config.newFileOnStartup) await ArchiveCurrentLogfileAsync().ConfiguredAwait();
            }
            catch (Exception e)
            {
                OptLog.ERROR(logHandle)?.Build($"Initializing file logger failed.", e);
            }
            while(!cts.IsCancellationRequested)
            {
                await UpdateConfigAsync().ConfiguredAwait();
                await receiver.WaitHandle.WaitAsync(cts.Token).ConfiguredAwait();
                if (cts.IsCancellationRequested) break;
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
                    if (config.logFileSizeLimitInMB * 1024 * 1024 <= GetLogFileSize()) await ArchiveCurrentLogfileAsync().ConfiguredAwait();
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
            logFileInfo.Directory?.Create();

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

        private Task ArchiveCurrentLogfileAsync()
        {
            return Task.Run(() =>
            {
                var logFileInfo = new FileInfo(config.logFilePath);
                logFileInfo.Directory?.Create();
                var archiveInfo = new FileInfo(config.archiveFilePath);
                archiveInfo.Directory?.Create();

                logFileInfo.Refresh();
                if (!logFileInfo.Exists) return;

                if (config.compressedArchive)
                {
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
                                if (overallLength > (long)config.logFilesArchiveLimitInMB * 1024 * 1024)
                                {
                                    entry.Delete();
                                }
                                else overallLength += entry.CompressedLength;
                            }
                        }
                    }
                    logFileInfo.Delete();
                }
                else
                {
                    string postfix = "_" + logFileInfo.CreationTime.ToString("yyyy-MM-dd_HH-mm-ss") + " until " + logFileInfo.LastWriteTime.ToString("yyyy-MM-dd_HH-mm-ss");
                    string archiveFilePath = config.logFilePath;
                    if (archiveFilePath.Contains('.')) archiveFilePath = archiveFilePath.InsertBefore(".", postfix);                
                    else archiveFilePath += postfix;

                    logFileInfo.MoveTo(archiveFilePath);

                    CleanupUncompressedArchiveFiles();
                }
            });
        }

        private void CleanupUncompressedArchiveFiles()
        {
            var logFileInfo = new FileInfo(config.logFilePath);
            var directory = logFileInfo.Directory;
            if (directory == null || !directory.Exists) return;

            string fileName = logFileInfo.Name;
            string extension = Path.GetExtension(fileName);
            string prefix = string.IsNullOrEmpty(extension) ? fileName : fileName.Substring(0, fileName.Length - extension.Length);
            string searchPattern = prefix + "*" + extension;

            List<FileInfo> archiveFiles = new List<FileInfo>();
            foreach (var file in directory.GetFiles(searchPattern))
            {
                // Skip the active log file that may get recreated concurrently.
                if (file.FullName.Equals(logFileInfo.FullName, StringComparison.OrdinalIgnoreCase)) continue;
                archiveFiles.Add(file);
            }

            // Newest first, so oldest files are the ones deleted when the limit is exceeded.
            archiveFiles.Sort((f1, f2) => f2.LastWriteTimeUtc.CompareTo(f1.LastWriteTimeUtc));

            long limit = (long)config.logFilesArchiveLimitInMB * 1024 * 1024;
            long overallLength = 0;
            foreach (var file in archiveFiles)
            {
                if (overallLength > limit) file.Delete();
                else overallLength += file.Length;
            }
        }
    }
}