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
        private readonly bool hasConsole = ConsoleHelper.CheckHasConsole();
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
            if (message == null) return;
            if (!hasConsole) return;

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

                    ConsoleHelper.UseLocked(() =>
                    {
                        var oldBgColor = Console.BackgroundColor;
                        var oldColor = Console.ForegroundColor;
                        Console.BackgroundColor = config.backgroundColor;
                        if (config.loglevelColors != null && config.loglevelColors.TryGetValue(logMessage.level, out var color)) Console.ForegroundColor = color;
                        
                        if (logMessage.level == Loglevel.ERROR) Console.Error.WriteLine(strMsg);
                        else Console.WriteLine(strMsg);

                        Console.ForegroundColor = oldColor;
                        Console.BackgroundColor = oldBgColor;
                    });
                }
            }
            else
            {
                Console.WriteLine(message.ToString());
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