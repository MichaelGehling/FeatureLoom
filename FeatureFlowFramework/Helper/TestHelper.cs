using FeatureFlowFramework.DataStorage;
using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public static class TestHelper
    {
        public static void PrepareTestContext(bool disconnectLoggers = true, bool useMemoryStorage = true)
        {
            ServiceContext.UseNewContexts();
            if (disconnectLoggers) Log.LogForwarder.DisconnectAll();
            if(useMemoryStorage)
            {
                Storage.DefaultReaderFactory = (category) => new MemoryStorage(category);
                Storage.DefaultWriterFactory = (category) => new MemoryStorage(category);
                Storage.RemoveAllReaderAndWriter();
            }
        }
    }
}
