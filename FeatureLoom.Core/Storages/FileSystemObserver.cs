﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using System;
using System.IO;

namespace FeatureLoom.Storages
{
    public class FileSystemObserver : IMessageSource<FileSystemObserver.ChangeNotification>, IDisposable
    {
        private SourceValueHelper sourceHelper;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher dirWatcher;
        private readonly string path;
        private readonly string filter;
        private readonly int bufferSize;
        private readonly bool includeSubdirectories;
        private readonly bool createDirectoriesIfNotExisting;
        private readonly NotifyFilters notifyFilters;

        public FileSystemObserver(string path,
                                  string filter = "",
                                  bool includeSubdirectories = false,
                                  NotifyFilters notifyFilters = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                                  bool createDirectoriesIfNotExisting = true,
                                  int bufferSize = 64 * 1024)
        {
            this.createDirectoriesIfNotExisting = createDirectoriesIfNotExisting;
            this.includeSubdirectories = includeSubdirectories;
            this.bufferSize = bufferSize.Clamp(4 * 1024, 64 * 1024);
            this.path = path;
            this.filter = filter;
            this.notifyFilters = notifyFilters;
            InitWatchers();
        }

        private void InitWatchers(bool reset = false)
        {
            if (createDirectoriesIfNotExisting) Directory.CreateDirectory(path);

            if (reset)
            {
                if (fileWatcher != null)
                {
                    fileWatcher.Dispose();
                    fileWatcher = null;
                }

                if (dirWatcher != null)
                {
                    dirWatcher.Dispose();
                    dirWatcher = null;
                }
            }

            fileWatcher = new FileSystemWatcher(path, filter);
            fileWatcher.IncludeSubdirectories = includeSubdirectories;
            fileWatcher.InternalBufferSize = bufferSize;
            fileWatcher.NotifyFilter = notifyFilters;
            fileWatcher.Changed += OnChangedFile;
            fileWatcher.Created += OnChangedFile;
            fileWatcher.Deleted += OnChangedFile;
            fileWatcher.Renamed += OnRenamedFile;
            fileWatcher.Error += OnError;
            fileWatcher.EnableRaisingEvents = true;
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Log.ERROR(this.GetHandle(), "FileSystemWatcher failed, will be reset!", e.GetException().ToString());
            InitWatchers(true);
        }

        private void OnRenamedFile(object sender, RenamedEventArgs e)
        {
            var message = new ChangeNotification(e.ChangeType, e.FullPath, e.OldFullPath);
            sourceHelper.ForwardAsync(message);
        }

        private void OnChangedFile(object sender, FileSystemEventArgs e)
        {
            var message = new ChangeNotification(e.ChangeType, e.FullPath);
            sourceHelper.ForwardAsync(message);
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type SentMessageType => typeof(ChangeNotification);

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void Dispose()
        {
            ((IDisposable)fileWatcher)?.Dispose();
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public readonly struct ChangeNotification
        {
            public readonly WatcherChangeTypes changeType;
            public readonly string path;
            public readonly string oldPath;

            public ChangeNotification(WatcherChangeTypes changeType, string path, string oldPath = null)
            {
                this.changeType = changeType;
                this.path = path;
                this.oldPath = oldPath ?? path;
            }
        }
    }
}