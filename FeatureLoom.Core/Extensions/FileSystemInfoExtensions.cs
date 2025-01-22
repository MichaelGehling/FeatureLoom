using System;
using System.IO;
using System.Threading.Tasks;

namespace FeatureLoom.Extensions
{
    public static class FileSystemInfoExtensions
    {
        public static FileSystemInfo RefreshAnd(this FileSystemInfo fileSystemInfo)
        {
            fileSystemInfo.Refresh();
            return fileSystemInfo;
        }

        public static FileInfoStatus GetFileInfoStatus(this FileInfo fileInfo, bool refresh = true)
        {
            if (refresh) fileInfo.Refresh();
            if (!fileInfo.Exists) return new FileInfoStatus();
            else return new FileInfoStatus(fileInfo.Length, fileInfo.CreationTime, fileInfo.LastWriteTime);
        }

        public static bool ChangedSince(this FileInfo fileInfo, FileInfoStatus oldStatus, bool refresh = true)
        {
            if (refresh) fileInfo.Refresh();
            if (fileInfo.Exists)
            {
                return fileInfo.Exists != oldStatus.exists ||
                       fileInfo.Length != oldStatus.length ||
                       fileInfo.CreationTime != oldStatus.creationTime ||
                       fileInfo.LastWriteTime != oldStatus.lastWriteTime;
            }
            else return oldStatus.exists;
        }

        // TODO: check if newer versions of .Net Core provides this method without using threadPool
        public async static Task<FileInfo[]> GetFilesAsync(this DirectoryInfo dir)
        {            
            return await Task.Run(() => dir.GetFiles()).ConfigureAwait(false);
        }

        // TODO: check if newer versions of .Net Core provides this method without using threadPool
        public async static Task<FileInfo[]> GetFilesAsync(this DirectoryInfo dir, string searchPattern, SearchOption searchOption)
        {
            return await Task.Run(() => dir.GetFiles(searchPattern, searchOption)).ConfigureAwait(false);
        }

        // TODO: check if newer versions of .Net Core provides this method without using threadPool
        public async static Task<DirectoryInfo[]> GetDirectoriesAsync(this DirectoryInfo dir)
        {
            return await Task.Run(() => dir.GetDirectories()).ConfigureAwait(false);
        }

        // TODO: check if newer versions of .Net Core provides this method without using threadPool
        public async static Task<DirectoryInfo[]> GetDirectoriesAsync(this DirectoryInfo dir, string searchPattern, SearchOption searchOption)
        {
            return await Task.Run(() => dir.GetDirectories(searchPattern, searchOption)).ConfigureAwait(false);
        }
    }

    public readonly struct FileInfoStatus
    {
        public readonly bool exists;
        public readonly long length;
        public readonly DateTime creationTime;
        public readonly DateTime lastWriteTime;

        public FileInfoStatus(long length, DateTime creationTime, DateTime lastWriteTime)
        {
            this.exists = true;
            this.length = length;
            this.creationTime = creationTime;
            this.lastWriteTime = lastWriteTime;
        }
    }
}