using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Storages
{
    public class StorageService
    {
        Dictionary<string, IStorageReader> categoryToReader = new Dictionary<string, IStorageReader>();
        FeatureLock categoryToReaderLock = new FeatureLock();
        Dictionary<string, IStorageWriter> categoryToWriter = new Dictionary<string, IStorageWriter>();
        FeatureLock categoryToWriterLock = new FeatureLock();
        Func<string, IStorageReader> createDefaultReader = category => new TextFileStorage(category);
        Func<string, IStorageWriter> createDefaultWriter = category => new TextFileStorage(category);

        public StorageService()
        {
            var configStorage = new TextFileStorage("config", new TextFileStorage.Config() { fileSuffix = ".json" });
            categoryToReader[configStorage.Category] = configStorage;
            categoryToWriter[configStorage.Category] = configStorage;

            var certificateStorage = new CertificateStorageReader("certificate");
            categoryToReader[certificateStorage.Category] = certificateStorage;
        }


        public Func<string, IStorageReader> DefaultReaderFactory { set => createDefaultReader = value; }
        public Func<string, IStorageWriter> DefaultWriterFactory { set => createDefaultWriter = value; }

        public void RemoveAllReaderAndWriter()
        {
            using (categoryToReaderLock.Lock())
            using (categoryToWriterLock.Lock())
            {
                foreach (IDisposable disposable in categoryToReader.Values.Where(item => item is IDisposable))
                {
                    disposable.Dispose();
                }
                categoryToReader.Clear();

                foreach (IDisposable disposable in categoryToWriter.Values.Where(item => item is IDisposable))
                {
                    disposable.Dispose();
                }
                categoryToWriter.Clear();
            }
        }

        public IStorageReader GetReader(string category)
        {
            using (var acquiredLock = categoryToReaderLock.LockReadOnly())
            {
                if (categoryToReader.TryGetValue(category, out IStorageReader reader)) return reader;
                else
                {
                    if (!(categoryToWriter.TryGetValue(category, out IStorageWriter writer) && writer is IStorageReader newReader))
                    {
                        newReader = createDefaultReader(category);
                    }
                    acquiredLock.UpgradeToWriteMode();
                    categoryToReader[category] = newReader;
                    return newReader;
                };
            }
        }

        public IStorageWriter GetWriter(string category)
        {
            using (var acquiredLock = categoryToWriterLock.LockReadOnly())
            {
                if (categoryToWriter.TryGetValue(category, out IStorageWriter writer)) return writer;
                else
                {
                    if (!(categoryToReader.TryGetValue(category, out IStorageReader reader) && reader is IStorageWriter newWriter))
                    {
                        newWriter = createDefaultWriter(category);
                    }
                    acquiredLock.UpgradeToWriteMode();
                    categoryToWriter[category] = newWriter;
                    return newWriter;
                };
            }
        }

        public void RegisterReader(IStorageReader reader)
        {
            using (categoryToReaderLock.Lock())
            {
                if (categoryToReader.TryGetValue(reader.Category, out var old) && old is IDisposable disposable) disposable.Dispose();
                categoryToReader[reader.Category] = reader;
            }
        }

        public void RegisterWriter(IStorageWriter writer)
        {
            using (categoryToWriterLock.Lock())
            {
                if (categoryToWriter.TryGetValue(writer.Category, out var old) && old is IDisposable disposable) disposable.Dispose();
                categoryToWriter[writer.Category] = writer;
            }
        }

        public void RegisterReaderWriter(IStorageReaderWriter readerWriter)
        {
            RegisterReader(readerWriter);
            RegisterWriter(readerWriter);
        }

        public bool HasCategoryReader(string category, bool acceptReaderWriter = true)
        {
            using (categoryToReaderLock.LockReadOnly())
            {
                if (categoryToReader.ContainsKey(category)) return true;
                else if (acceptReaderWriter && HasCategoryWriter(category, false) && GetWriter(category) is IStorageReader) return true;
                else return false;
            }
        }

        public bool HasCategoryWriter(string category, bool acceptReaderWriter = true)
        {
            using (categoryToWriterLock.LockReadOnly())
            {
                if (categoryToWriter.ContainsKey(category)) return true;
                else if (acceptReaderWriter && HasCategoryReader(category, false) && GetReader(category) is IStorageWriter) return true;
                else return false;
            }
        }
    }
}