using FeatureLoom.DependencyInversion;
using System;

namespace FeatureLoom.Storages
{
    public static class Storage
    {
        public static Func<string, IStorageReader> DefaultReaderFactory { set => Service<StorageService>.Instance.DefaultReaderFactory = value; }
        public static Func<string, IStorageWriter> DefaultWriterFactory { set => Service<StorageService>.Instance.DefaultWriterFactory = value; }

        public static IStorageReader GetReader(string category) => Service<StorageService>.Instance.GetReader(category);

        public static IStorageWriter GetWriter(string category) => Service<StorageService>.Instance.GetWriter(category);

        public static bool HasCategoryReader(string category, bool acceptReaderWriter = true) => Service<StorageService>.Instance.HasCategoryReader(category, acceptReaderWriter);

        public static bool HasCategoryWriter(string category, bool acceptReaderWriter = true) => Service<StorageService>.Instance.HasCategoryWriter(category, acceptReaderWriter);

        public static void RegisterReader(IStorageReader reader) => Service<StorageService>.Instance.RegisterReader(reader);

        public static void RegisterReaderWriter(IStorageReaderWriter readerWriter) => Service<StorageService>.Instance.RegisterReaderWriter(readerWriter);

        public static void RegisterWriter(IStorageWriter writer) => Service<StorageService>.Instance.RegisterWriter(writer);

        public static void RemoveAllReaderAndWriter() => Service<StorageService>.Instance.RemoveAllReaderAndWriter();
    }
}