using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Logging
{
    public class ConsoleLogger : IMessageSink
    {
        private StringBuilder stringBuilder = new StringBuilder();
        private MicroLock stringBuilderLock = new MicroLock();

        public class Config : Configuration
        {
            public string format = "| {0} | ctxt{4} | thrd{3} | {1} | {2}| {8} |";
            public string timeStampFormat = "HH:mm:ss.ffff";
            public Loglevel loglevel = Loglevel.WARNING;
            public Dictionary<Loglevel, ConsoleColor> loglevelColors = new Dictionary<Loglevel, ConsoleColor>() 
            {
                [Loglevel.FORCE] = ConsoleColor.Cyan,
                [Loglevel.ERROR] = ConsoleColor.Red,
                [Loglevel.WARNING] = ConsoleColor.Yellow,
                [Loglevel.INFO] = ConsoleColor.White,
                [Loglevel.DEBUG] = ConsoleColor.Gray,
                [Loglevel.TRACE] = ConsoleColor.DarkGray,
            };
            public ConsoleColor backgroundColor = ConsoleColor.Black;            
        }

        public Config config = new Config();

        public void Post<M>(in M message)
        {
            if (!ConsoleHelper.CheckHasConsole()) return;
            if (message == null) return;            

            config.TryUpdateFromStorage(true);

            if (message is LogMessage logMessage)
            {
                if (logMessage.level <= config.loglevel)
                {
                    string strMsg;
                    using (stringBuilderLock.Lock())
                    {
                        strMsg = logMessage.PrintToStringBuilder(stringBuilder, config?.format, config?.timeStampFormat).ToString();
                        stringBuilder?.Clear();
                    }

                    ConsoleColor? fgColor = null;
                    ConsoleColor? bgColor = config.backgroundColor;
                    if (config.loglevelColors != null && config.loglevelColors.TryGetValue(logMessage.level, out var color)) fgColor = color;

                    if (logMessage.level == Loglevel.ERROR) ConsoleHelper.WriteLineToError(strMsg, fgColor, bgColor);
                    else ConsoleHelper.WriteLine(strMsg, fgColor, bgColor);
                }
            }
            else
            {
                ConsoleHelper.WriteLine(message.ToString());
            }
        }

        public void Post<M>(M message)
        {
            Post(in message);
        }

        

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }
    }
}