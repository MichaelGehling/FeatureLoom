using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Services.Logging;
using System;
using System.IO;

namespace FeatureFlowFramework.Services.DataStorage
{
    public class FileSystemObserver : IDataFlowSource, IDisposable
    {
        private DataFlowSourceHelper sourceHelper = new DataFlowSourceHelper();
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
            if(createDirectoriesIfNotExisting) Directory.CreateDirectory(path);

            if(reset)
            {
                if(fileWatcher != null)
                {
                    fileWatcher.Dispose();
                    fileWatcher = null;
                }

                if(dirWatcher != null)
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
            Log.ERROR(this, "FileSystemWatcher failed, will be reset!", e.GetException().ToString());
            InitWatchers(true);
        }

        private void OnRenamedFile(object sender, RenamedEventArgs e)
        {
            var message = new ChangeNotification(e.ChangeType, e.FullPath, e.OldFullPath);
            sourceHelper.Forward(message);
        }

        private void OnChangedFile(object sender, FileSystemEventArgs e)
        {
            var message = new ChangeNotification(e.ChangeType, e.FullPath);
            sourceHelper.Forward(message);
        }

        public int CountConnectedSinks => ((IDataFlowSource)sourceHelper).CountConnectedSinks;
        public void DisconnectAll()
        {
            ((IDataFlowSource)sourceHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).DisconnectFrom(sink);
        }

        public void Dispose()
        {
            ((IDisposable)fileWatcher)?.Dispose();
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sourceHelper).GetConnectedSinks();
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
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