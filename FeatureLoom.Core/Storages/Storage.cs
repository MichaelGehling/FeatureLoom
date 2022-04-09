﻿using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Storages
{
    public static class Storage
    {
        private class ContextData : IServiceContextData
        {
            public Dictionary<string, IStorageReader> categoryToReader = new Dictionary<string, IStorageReader>();
            public FeatureLock categoryToReaderLock = new FeatureLock();
            public Dictionary<string, IStorageWriter> categoryToWriter = new Dictionary<string, IStorageWriter>();
            public FeatureLock categoryToWriterLock = new FeatureLock();
            public Func<string, IStorageReader> createDefaultReader = category => new TextFileStorage(category);
            public Func<string, IStorageWriter> createDefaultWriter = category => new TextFileStorage(category);

            public ContextData()
            {
                var configStorage = new TextFileStorage("config", new TextFileStorage.Config() { fileSuffix = ".json" });
                categoryToReader[configStorage.Category] = configStorage;
                categoryToWriter[configStorage.Category] = configStorage;

                var certificateStorage = new CertificateStorageReader("certificate");
                categoryToReader[certificateStorage.Category] = certificateStorage;
            }

            public IServiceContextData Copy()
            {
                ContextData newContext = new ContextData()
                {
                    categoryToReader = new Dictionary<string, IStorageReader>(this.categoryToReader),
                    categoryToWriter = new Dictionary<string, IStorageWriter>(this.categoryToWriter),
                    createDefaultReader = this.createDefaultReader,
                    createDefaultWriter = this.createDefaultWriter
                };
                return newContext;
            }
        }

        private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static Func<string, IStorageReader> DefaultReaderFactory { set => context.Data.createDefaultReader = value; }
        public static Func<string, IStorageWriter> DefaultWriterFactory { set => context.Data.createDefaultWriter = value; }

        public static void RemoveAllReaderAndWriter()
        {
            var contextData = context.Data;
            using (contextData.categoryToReaderLock.Lock())
            using (contextData.categoryToWriterLock.Lock())
            {
                foreach (IDisposable disposable in contextData.categoryToReader.Values.Where(item => item is IDisposable))
                {
                    disposable.Dispose();
                }
                contextData.categoryToReader.Clear();

                foreach (IDisposable disposable in contextData.categoryToWriter.Values.Where(item => item is IDisposable))
                {
                    disposable.Dispose();
                }                
                contextData.categoryToWriter.Clear();
            }
        }

        public static IStorageReader GetReader(string category)
        {
            var contextData = context.Data;
            using (var acquiredLock = contextData.categoryToReaderLock.LockReadOnly())
            {
                if (contextData.categoryToReader.TryGetValue(category, out IStorageReader reader)) return reader;
                else
                {
                    if (!(contextData.categoryToWriter.TryGetValue(category, out IStorageWriter writer) && writer is IStorageReader newReader))
                    {
                        newReader = contextData.createDefaultReader(category);
                    }
                    acquiredLock.UpgradeToWriteMode();
                    contextData.categoryToReader[category] = newReader;
                    return newReader;
                };
            }
        }

        public static IStorageWriter GetWriter(string category)
        {
            var contextData = context.Data;
            using (var acquiredLock = contextData.categoryToWriterLock.LockReadOnly())
            {
                if (contextData.categoryToWriter.TryGetValue(category, out IStorageWriter writer)) return writer;
                else
                {
                    if (!(contextData.categoryToReader.TryGetValue(category, out IStorageReader reader) && reader is IStorageWriter newWriter))
                    {
                        newWriter = contextData.createDefaultWriter(category);
                    }
                    acquiredLock.UpgradeToWriteMode();
                    contextData.categoryToWriter[category] = newWriter;
                    return newWriter;
                };
            }
        }

        public static void RegisterReader(IStorageReader reader)
        {
            var contextData = context.Data;
            using (contextData.categoryToReaderLock.Lock())
            {
                if (contextData.categoryToReader.TryGetValue(reader.Category, out var old) && old is IDisposable disposable) disposable.Dispose();
                contextData.categoryToReader[reader.Category] = reader;
            }
        }

        public static void RegisterWriter(IStorageWriter writer)
        {
            var contextData = context.Data;
            using (contextData.categoryToWriterLock.Lock())
            {
                if (contextData.categoryToWriter.TryGetValue(writer.Category, out var old) && old is IDisposable disposable) disposable.Dispose();
                contextData.categoryToWriter[writer.Category] = writer;
            }
        }

        public static void RegisterReaderWriter(IStorageReaderWriter readerWriter)
        {
            RegisterReader(readerWriter);
            RegisterWriter(readerWriter);
        }

        public static bool HasCategoryReader(string category, bool acceptReaderWriter = true)
        {
            var contextData = context.Data;
            using (contextData.categoryToReaderLock.LockReadOnly())
            {
                if (contextData.categoryToReader.ContainsKey(category)) return true;
                else if (acceptReaderWriter && HasCategoryWriter(category, false) && GetWriter(category) is IStorageReader) return true;
                else return false;
            }
        }

        public static bool HasCategoryWriter(string category, bool acceptReaderWriter = true)
        {
            var contextData = context.Data;
            using (contextData.categoryToWriterLock.LockReadOnly())
            {
                if (contextData.categoryToWriter.ContainsKey(category)) return true;
                else if (acceptReaderWriter && HasCategoryReader(category, false) && GetReader(category) is IStorageWriter) return true;
                else return false;
            }
        }
    }
}