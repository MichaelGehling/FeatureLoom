using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureFlowFramework.Services.DataStorage
{
    public static class Storage
    {
        class Context : IServiceContextData
        {
            public Dictionary<string, IStorageReader> categoryToReader = new Dictionary<string, IStorageReader>();
            public FeatureLock categoryToReaderLock = new FeatureLock();
            public Dictionary<string, IStorageWriter> categoryToWriter = new Dictionary<string, IStorageWriter>();
            public FeatureLock categoryToWriterLock = new FeatureLock();
            public Func<string, IStorageReader> createDefaultReader = category => new TextFileStorage(category);
            public Func<string, IStorageWriter> createDefaultWriter = category => new TextFileStorage(category);

            public Context()
            {
                var configStorage = new TextFileStorage("config", new TextFileStorage.Config() { fileSuffix = ".json" });
                categoryToReader[configStorage.Category] = configStorage;
                categoryToWriter[configStorage.Category] = configStorage;

                var certificateStorage = new CertificateStorageReader("certificate");
                categoryToReader[certificateStorage.Category] = certificateStorage;
            }

            public IServiceContextData Copy()
            {
                Context newContext = new Context()
                {
                    categoryToReader = new Dictionary<string, IStorageReader>(this.categoryToReader),
                    categoryToWriter = new Dictionary<string, IStorageWriter>(this.categoryToWriter),
                    createDefaultReader = this.createDefaultReader,
                    createDefaultWriter = this.createDefaultWriter
                };
                return newContext;
            }
        }
        static ServiceContext<Context> context = new ServiceContext<Context>();
        

        public static Func<string, IStorageReader> DefaultReaderFactory { set => context.Data.createDefaultReader = value; }
        public static Func<string, IStorageWriter> DefaultWriterFactory { set => context.Data.createDefaultWriter = value; }

        public static void RemoveAllReaderAndWriter()
        {
            var contextData = context.Data;
            using(contextData.categoryToReaderLock.ForWriting())
            {
                contextData.categoryToReader.Clear();
            }
            using(contextData.categoryToWriterLock.ForWriting())
            {
                contextData.categoryToWriter.Clear();
            }
        }

        public static IStorageReader GetReader(string category)
        {
            var contextData = context.Data;
            using(contextData.categoryToReaderLock.ForWriting())
            {
                if(contextData.categoryToReader.TryGetValue(category, out IStorageReader reader)) return reader;
                else
                {
                    if(!(contextData.categoryToWriter.TryGetValue(category, out IStorageWriter writer) && writer is IStorageReader newReader))
                    {
                        newReader = contextData.createDefaultReader(category);
                    }
                    contextData.categoryToReader[category] = newReader;
                    return newReader;
                };
            }
        }

        public static IStorageWriter GetWriter(string category)
        {
            var contextData = context.Data;
            using (contextData.categoryToWriterLock.ForWriting())
            {
                if(contextData.categoryToWriter.TryGetValue(category, out IStorageWriter writer)) return writer;
                else
                {
                    if(!(contextData.categoryToReader.TryGetValue(category, out IStorageReader reader) && reader is IStorageWriter newWriter))
                    {
                        newWriter = contextData.createDefaultWriter(category);
                    }
                    contextData.categoryToWriter[category] = newWriter;
                    return newWriter;
                };
            }
        }

        public static void RegisterReader(IStorageReader reader)
        {
            var contextData = context.Data;
            using (contextData.categoryToReaderLock.ForWriting())
            {
                contextData.categoryToReader[reader.Category] = reader;
            }
        }

        public static void RegisterWriter(IStorageWriter writer)
        {
            var contextData = context.Data;
            using (contextData.categoryToWriterLock.ForWriting())
            {
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
            using (contextData.categoryToReaderLock.ForReading())
            {
                if(contextData.categoryToReader.ContainsKey(category)) return true;
                else if(acceptReaderWriter && HasCategoryWriter(category, false) && GetWriter(category) is IStorageReader) return true;
                else return false;
            }
        }

        public static bool HasCategoryWriter(string category, bool acceptReaderWriter = true)
        {
            var contextData = context.Data;
            using (contextData.categoryToWriterLock.ForReading())
            {
                if(contextData.categoryToWriter.ContainsKey(category)) return true;
                else if(acceptReaderWriter && HasCategoryReader(category, false) && GetReader(category) is IStorageWriter) return true;
                else return false;
            }
        }

    }
}