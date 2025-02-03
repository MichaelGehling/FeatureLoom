using FeatureLoom.MessageFlow;
using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Serialization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.InteropServices;

namespace FeatureLoom.Storages
{
    public class TextFileStorage : IStorageReaderWriter, IDisposable
    {
        public class Config : Configuration
        {
            public bool useCategoryFolder = true;
            public string basePath = ".";
            public bool preventEscapingRootPath = true;
            public string fileSuffix = "";
            public bool allowSubscription = true;
            public TimeSpan timeout = TimeSpan.Zero;
            public TimeSpan duplicateFileEventSuppressionTime = 100.Milliseconds();
            public bool updateCacheForSubscription = true;
            public bool updateCacheOnRead = true;
            public bool updateCacheOnWrite = true;
            public bool logFailedDeserialization = true;
            public TimeSpan fileChangeNotificationDelay = 0.3.Seconds();
            public InMemoryCache<string, string>.CacheSettings cacheSettings = new InMemoryCache<string, string>.CacheSettings();
        }

        private Config config;
        private readonly string category;
        private DirectoryInfo rootDir;
        private readonly InMemoryCache<string, string> cache;
        private HashSet<string> fileSet = new HashSet<string>();
        private StorageSubscriptions subscriptions = new StorageSubscriptions();
        public string Category => category;

        private bool fileSystemObservationActive = false;
        private FileSystemObserver fileObserver;
        private ProcessingEndpoint<FileSystemObserver.ChangeNotification> fileChangeProcessor;
        private DuplicateMessageSuppressor duplicateMessageSuppressor;

        private bool useWindowsPaths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private string validPathSeperator = "/";
        private string invalidPathSeperator = "\\";

        public TextFileStorage(string category, Config config = default)
        {
            this.category = category;
            this.config = config ?? new Config();
            config = this.config;

            if (useWindowsPaths) SwapHelper.Swap(ref validPathSeperator, ref invalidPathSeperator);
            config.basePath = config.basePath.Replace(invalidPathSeperator, validPathSeperator);
            config.fileSuffix = config.fileSuffix.Replace(invalidPathSeperator, validPathSeperator);
            
            if (config.IsUriDefault) config.Uri = "TextFileStorageConfig" + "_" + this.category;

            if (config.ConfigCategory != this.category) config.TryUpdateFromStorage(false);

            string basePath = config.basePath;
            if (config.useCategoryFolder) basePath = Path.Combine(config.basePath, this.category);
            rootDir = new DirectoryInfo(basePath);

            lock (fileSet)
            {
                fileSet = CreateNewFileSet();
            }
            if (config.updateCacheForSubscription || config.updateCacheOnRead || config.updateCacheOnWrite)
            {
                cache = new InMemoryCache<string, string>(str => Encoding.UTF8.GetByteCount(str), config.cacheSettings);
                ActivateFileSystemObservation(true);
            }
        }

        private void ActivateFileSystemObservation(bool activate)
        {
            if (activate && !fileSystemObservationActive)
            {
                fileObserver = new FileSystemObserver(rootDir.FullName, "*" + config.fileSuffix, true);
                duplicateMessageSuppressor = new DuplicateMessageSuppressor(config.duplicateFileEventSuppressionTime, (m1, m2) =>
                {
                    if (m1 is FileSystemObserver.ChangeNotification notification1 &&
                        m2 is FileSystemObserver.ChangeNotification notification2 &&
                        notification1.changeType == WatcherChangeTypes.Created &&
                        notification2.changeType == WatcherChangeTypes.Changed &&
                        notification1.path == notification2.path)
                    {
                        return true;
                    }
                    else return m1.Equals(m2);
                });
                fileChangeProcessor = new ProcessingEndpoint<FileSystemObserver.ChangeNotification>(async msg => await ProcessChangeNotification(msg).ConfigureAwait(false));
                fileObserver.ConnectTo(duplicateMessageSuppressor).ConnectTo(fileChangeProcessor);
                fileSystemObservationActive = true;
            }
            else if (!activate && fileSystemObservationActive)
            {
                fileObserver.Dispose();
                fileObserver = null;
                duplicateMessageSuppressor = null;
                fileChangeProcessor = null;
                fileSystemObservationActive = false;
            }
        }

        private async Task ProcessChangeNotification(FileSystemObserver.ChangeNotification notification)
        {
            if (subscriptions.Count == 0 && cache == null)
            {
                ActivateFileSystemObservation(false);
            }

            await Task.Delay(config.fileChangeNotificationDelay).ConfigureAwait(false);

            if (notification.changeType.IsFlagSet(WatcherChangeTypes.Deleted))
            {
                ProcessChangeNotification_Delete(notification);
            }
            else
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(notification.path);

                if (!directoryInfo.Attributes.IsFlagSet(FileAttributes.Directory))
                {
                    ProcessChangeNotification_File(notification);
                }
                else
                {
                    ProcessChangeNotification_Directory(notification, directoryInfo);
                }
            }
        }


        private void ProcessChangeNotification_Directory(FileSystemObserver.ChangeNotification notification, DirectoryInfo directoryInfo)
        {
            if (notification.changeType.IsFlagSet(WatcherChangeTypes.Created))
            {
                var addedFileInfos = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Where(fileInfo => fileInfo.FullName.MatchesWildcard("*" + config.fileSuffix));
                foreach (var fileInfo in addedFileInfos)
                {
                    lock (fileSet)
                    {
                        fileSet.Add(fileInfo.FullName);
                    }
                    string uri = FilePathToUri(fileInfo.FullName);
                    NotifySubscriptions(uri, UpdateEvent.Created);
                }
            }
            else if (notification.changeType.IsFlagSet(WatcherChangeTypes.Renamed))
            {
                UpdateOnRemovedDir();

                var addedFileInfos = directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Where(fileInfo => fileInfo.FullName.MatchesWildcard("*" + config.fileSuffix));
                foreach (var fileInfo in addedFileInfos)
                {
                    lock (fileSet)
                    {
                        fileSet.Add(fileInfo.FullName);
                    }
                    string uri = FilePathToUri(fileInfo.FullName);
                    NotifySubscriptions(uri, UpdateEvent.Created);
                }
            }
        }

        private void ProcessChangeNotification_File(FileSystemObserver.ChangeNotification notification)
        {
            if (notification.changeType.IsFlagSet(WatcherChangeTypes.Changed))
            {
                lock (fileSet)
                {
                    fileSet.Add(notification.path);
                }
                string uri = FilePathToUri(notification.path);
                if (uri != null)
                {
                    cache?.Remove(uri);
                    NotifySubscriptions(uri, UpdateEvent.Updated);
                }
            }
            else if (notification.changeType.IsFlagSet(WatcherChangeTypes.Created))
            {
                lock (fileSet)
                {
                    fileSet.Add(notification.path);
                }
                string uri = FilePathToUri(notification.path);
                if (uri != null)
                {
                    NotifySubscriptions(uri, UpdateEvent.Created);
                }
            }
            else if (notification.changeType.IsFlagSet(WatcherChangeTypes.Renamed))
            {
                lock (fileSet)
                {
                    fileSet.Remove(notification.oldPath);
                    fileSet.Add(notification.path);
                }
                string oldUri = FilePathToUri(notification.oldPath);
                if (oldUri != null)
                {
                    cache?.Remove(oldUri);
                    NotifySubscriptions(oldUri, UpdateEvent.Removed);
                }
                string newUri = FilePathToUri(notification.path);
                if (newUri != null)
                {
                    NotifySubscriptions(newUri, UpdateEvent.Created);
                }
            }
        }

        private void ProcessChangeNotification_Delete(FileSystemObserver.ChangeNotification notification)
        {
            bool removed;
            lock (fileSet)
            {
                removed = fileSet.Remove(notification.path);
            }
            if (removed)
            {
                string uri = FilePathToUri(notification.path);
                cache?.Remove(uri);
                NotifySubscriptions(uri, UpdateEvent.Removed);
            }
            else
            {
                UpdateOnRemovedDir();
            }
        }

        private void UpdateOnRemovedDir()
        {
            var newFileSet = CreateNewFileSet();
            lock (fileSet)
            {
                fileSet.ExceptWith(newFileSet);
                foreach (var fileName in fileSet)
                {
                    string uri = FilePathToUri(fileName);
                    cache?.Remove(uri);
                    NotifySubscriptions(uri, UpdateEvent.Removed);
                }
                fileSet = newFileSet;
            }
        }

        private void UpdateCacheForSubscriptions(string uri)
        {
            if (config.updateCacheForSubscription)
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    try
                    {
                        using (var stream = fileInfo.OpenText())
                        {
                            if (config.timeout > TimeSpan.Zero) stream.BaseStream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                            var fileContent = stream.ReadToEnd();
                            cache?.Add(uri, fileContent);
                        }
                    }
                    catch (Exception e)
                    {
                        cache?.Remove(uri);
                        OptLog.WARNING()?.Build($"Failed reading file {filePath} to cache. Cache entry was invalidated", e.ToString());
                    }
                }
            }
        }

        private HashSet<string> CreateNewFileSet()
        {
            if (!rootDir.RefreshAnd().Exists) return new HashSet<string>();

            var fileInfos = rootDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(fileInfo => fileInfo.FullName.MatchesWildcard("*" + config.fileSuffix));
            var newFileSet = new HashSet<string>();
            foreach (var info in fileInfos)
            {
                newFileSet.Add(info.FullName);
            }
            return newFileSet;
        }

        private void NotifySubscriptions(string uri, UpdateEvent updateEvent, bool updateCache = true)
        {
            if (subscriptions.Notify(uri, this.category, updateEvent) && updateCache)
            {
                UpdateCacheForSubscriptions(uri);
            }
        }

        private readonly struct Subscription
        {
            public readonly string uriPattern;
            public readonly Sender sender;

            public Subscription(string uriPattern, Sender sender)
            {
                this.uriPattern = uriPattern;
                this.sender = sender;
            }
        }

        public bool TrySubscribeForChangeNotifications(string uriPattern, IMessageSink<ChangeNotification> notificationSink)
        {
            ActivateFileSystemObservation(true);
            subscriptions.Add(uriPattern, notificationSink);
            return true;
        }

        protected virtual string FilePathToUri(string filePath)
        {
            if (!filePath.StartsWith(rootDir.FullName) || !filePath.EndsWith(config.fileSuffix)) return null;
            string basePath = rootDir.FullName;
            if (!basePath.EndsWith(validPathSeperator)) basePath += validPathSeperator;
            filePath.TryExtract($"{basePath}{{rawUri}}{config.fileSuffix}", out string rawUri);            
            return rawUri.Replace('\\', '/');
        }

        protected virtual string UriToFilePath(string uri)
        {
            string resultingPath = Path.GetFullPath(Path.Combine(rootDir.FullName, $"{uri}{config.fileSuffix}"));
            if (config.preventEscapingRootPath && !resultingPath.StartsWith(rootDir.FullName))
            {
                OptLog.WARNING()?.Build($"Resulting path ({resultingPath}) was not inside root path ({rootDir.FullName})");
                throw new Exception($"Resulting path ({resultingPath}) was not inside root path ({rootDir.FullName})");
            }
            return resultingPath;
        }

        protected virtual bool TryDeserialize<T>(string str, out T data)
        {
            data = default;

            if (str is T strObj)
            {
                data = strObj;
                return true;
            }
            else
            {
                if (JsonHelper.DefaultDeserializer.TryDeserialize<T>(str, out data)) return true;

                if (config.logFailedDeserialization) OptLog.WARNING()?.Build($"Failed on deserializing to type {typeof(T)}!");
                data = default;
                return false;
            }
        }

        protected virtual bool TrySerialize<T>(T data, out string str)
        {
            if (data is string strData)
            {
                str = strData;
                return true;
            }
            else
            {                
                try
                {
                    str = JsonHelper.DefaultSerializer.Serialize(data);
                    return true;
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build("Failed serializing persiting object", e.ToString());
                    str = default;
                    return false;
                }
            }
        }

        public async Task<(bool, string[])> TryListUrisAsync(string pattern = null)
        {
            try
            {
                if (this.fileSystemObservationActive)
                {
                    lock (fileSet)
                    {
                        if (fileSet.Count == 0) return (true, Array.Empty<string>());

                        var uris = new List<string>();
                        foreach (var fileName in fileSet)
                        {
                            string uri = FilePathToUri(fileName);
                            if (pattern == null || uri.MatchesWildcard(pattern))
                            {
                                uris.Add(uri);
                            }
                        }
                        return (true, uris.ToArray());
                    }
                }
                else
                {
                    if (!rootDir.RefreshAnd().Exists) return (true, Array.Empty<string>());

                    var uris = new List<string>();
                    var files = rootDir.EnumerateFiles("*", SearchOption.AllDirectories).Where(fileInfo => fileInfo.FullName.MatchesWildcard("*"+ config.fileSuffix));
                    foreach (var file in files)
                    {
                        string uri = FilePathToUri(file.FullName);
                        if (pattern == null || uri.MatchesWildcard(pattern))
                        {
                            uris.Add(uri);
                        }
                    }
                    return (true, uris.ToArray());
                }
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build("Reading files to retreive Uris failed!", e.ToString());
                return (false, null);
            }
        }

        public bool Exists(string uri)
        {
            if (cache?.Contains(uri) ?? false) return true;

            string filePath = UriToFilePath(uri);
            return File.Exists(filePath);
        }

        public async Task<(bool, T)> TryReadAsync<T>(string uri)
        {
            try
            {
                T data = default;
                bool success = false;

                if (cache != null && cache.TryGet(uri, out string cacheString))
                {
                    success = TryDeserialize(cacheString, out data);
                    if (!success)
                    {
                        OptLog.WARNING()?.Build($"Failed deserializing cached value for URI {uri}. Will removed from cache!");
                        cache.Remove(uri);
                    }
                    return (success, data);
                }

                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    using (var stream = fileInfo.OpenText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        var fileContent = await stream.ReadToEndAsync().ConfigureAwait(false);

                        success = TryDeserialize(fileContent, out data);

                        if (!success) OptLog.WARNING()?.Build($"Failed deserializing value for URI {uri}! (It will not be cached)");
                        else if (config.updateCacheOnRead)
                        {
                            cache?.Add(uri, fileContent);
                        }
                    }
                }

                return (success, data);
            }
            catch(Exception e)
            {
                OptLog.ERROR()?.Build("Reading file failed!", e.ToString());
                return (false, default);
            }
        }

        public async Task<bool> TryReadAsync(string uri, Func<Stream, Task> consumer)
        {
            try
            {
                if (cache != null && cache.TryGet(uri, out string cacheString))
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(cacheString));
                    await consumer(stream).ConfigureAwait(false);
                    return true;
                }

                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    using (var stream = fileInfo.OpenRead())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.ReadTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();

                        if (config.updateCacheOnRead)
                        {
                            using (var memoryStream = new MemoryStream())
                            using (var textReader = new StreamReader(memoryStream))
                            {
                                await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
                                memoryStream.Position = 0;
                                await consumer(memoryStream).ConfigureAwait(false);
                                memoryStream.Position = 0;
                                string fileContent = await textReader.ReadToEndAsync().ConfigureAwait(false);
                                cache?.Add(uri, fileContent);
                            }
                        }
                        else
                        {
                            await consumer(stream).ConfigureAwait(false);
                        }

                        return true;
                    }
                }
                return false;
            }
            catch(Exception e)
            {
                OptLog.ERROR()?.Build("Reading file failed!", e.ToString());
                return false;
            }
        }

        public async Task<bool> TryWriteAsync<T>(string uri, T data)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();
                bool created = !fileInfo.Exists;

                if (TrySerialize(data, out string fileContent))
                {
                    if (config.updateCacheOnWrite || (config.updateCacheForSubscription && fileSystemObservationActive))
                    {
                        cache?.Add(uri, fileContent);
                    }

                    if (fileSystemObservationActive)
                    {
                        if (created) duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Created, fileInfo.FullName, fileInfo.FullName));
                        else duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Changed, fileInfo.FullName, fileInfo.FullName));
                    }

                    using (var stream = fileInfo.CreateText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        await stream.WriteAsync(fileContent).ConfigureAwait(false);
                        lock (fileSet)
                        {
                            fileSet.Add(fileInfo.FullName);
                        }                        
                    }

                    if (fileSystemObservationActive)
                    {
                        UpdateEvent updateEvent = created ? UpdateEvent.Created : UpdateEvent.Updated;
                        NotifySubscriptions(uri, updateEvent, false);                                                
                    }

                    return true;
                }
                else return false;
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public async Task<bool> TryWriteAsync(string uri, Stream sourceStream)
        {
            bool disposeStream = false;
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();
                bool created = !fileInfo.Exists;

                if (config.updateCacheOnWrite || (config.updateCacheForSubscription && fileSystemObservationActive))
                {
                    if (!sourceStream.CanSeek)
                    {
                        MemoryStream memoryStream = new MemoryStream();
                        await sourceStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                        sourceStream = memoryStream;
                        sourceStream.Position = 0;
                        disposeStream = true;
                    }
                    string fileContent;
                    var origPosition = sourceStream.Position;
                    // Without using, otherwise the streamreader would dispose the stream already here!
                    var textReader = new StreamReader(sourceStream);
                    fileContent = await textReader.ReadToEndAsync().ConfigureAwait(false);
                    sourceStream.Position = origPosition;
                    cache?.Add(uri, fileContent);                                        
                }

                if (fileSystemObservationActive)
                {
                    if (created) duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Created, fileInfo.FullName, fileInfo.FullName));
                    else duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Changed, fileInfo.FullName, fileInfo.FullName));
                }

                using (var stream = fileInfo.OpenWrite())
                {
                    if (config.timeout > TimeSpan.Zero) stream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                    stream.SetLength(0);
                    await sourceStream.CopyToAsync(stream).ConfigureAwait(false);                    

                    lock (fileSet)
                    {
                        fileSet.Add(fileInfo.FullName);
                    }
                }

                if (fileSystemObservationActive)
                {
                    UpdateEvent updateEvent = created ? UpdateEvent.Created : UpdateEvent.Updated;
                    NotifySubscriptions(uri, updateEvent, false);                        
                }
                return true;
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
            finally
            {
                if (disposeStream) sourceStream.Dispose();
            }
        }

        public async Task<bool> TryAppendAsync<T>(string uri, T data)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                if (TrySerialize(data, out string fileContent))
                {
                    cache?.Remove(uri);

                    using (var stream = fileInfo.AppendText())
                    {
                        if (config.timeout > TimeSpan.Zero) stream.BaseStream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                        await stream.WriteAsync(fileContent).ConfigureAwait(false);
                        return true;
                    }

                }
                else return false;
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public async Task<bool> TryAppendAsync(string uri, Stream sourceStream)
        {
            try
            {
                string filePath = UriToFilePath(uri);
                FileInfo fileInfo = new FileInfo(filePath);
                fileInfo.Directory.Create();

                cache?.Remove(uri);

                using (var stream = fileInfo.OpenWrite())
                {
                    if (config.timeout > TimeSpan.Zero) stream.WriteTimeout = config.timeout.TotalMilliseconds.ToIntTruncated();
                    stream.Seek(0, SeekOrigin.End);
                    await sourceStream.CopyToAsync(stream).ConfigureAwait(false);
                    return true;
                }
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed writing file for uri {uri}!", e.ToString());
                return false;
            }
        }

        public Task<bool> TryDeleteAsync(string uri)
        {
            string filePath = UriToFilePath(uri);
            FileInfo fileInfo = new FileInfo(filePath);

            try
            {
                cache?.Remove(uri);

                if (fileInfo.Exists)
                {
                    if (fileSystemObservationActive)
                    {
                        duplicateMessageSuppressor?.AddSuppressor(new FileSystemObserver.ChangeNotification(WatcherChangeTypes.Deleted, fileInfo.FullName, fileInfo.FullName));                        
                    }

                    fileInfo.Delete();
                    lock (fileSet)
                    {
                        fileSet.Remove(fileInfo.FullName);
                    }

                    if (fileSystemObservationActive)
                    {
                        NotifySubscriptions(uri, UpdateEvent.Removed, false);
                    }
                }
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                OptLog.ERROR()?.Build($"Failed on deleting file at {fileInfo.ToString()}", e.ToString());
                return Task.FromResult(false);
            }
        }

        public void Dispose()
        {
            ((IDisposable)fileObserver)?.Dispose();
        }

        private class FileSubscriptionStatus
        {
            public FileInfo fileInfo;
            public FileInfoStatus fileInfoStatus;

            public FileSubscriptionStatus(string filePath)
            {
                fileInfo = new FileInfo(filePath);
                fileInfoStatus = fileInfo.GetFileInfoStatus();
            }

            public bool Changed
            {
                get
                {
                    bool changed = fileInfo.ChangedSince(fileInfoStatus);
                    if (changed) fileInfoStatus = fileInfo.GetFileInfoStatus();
                    return changed;
                }
            }
        }
    }
}