using FeatureLoom.MessageFlow;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using System;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Logging
{
    public class ConsoleLogger : IMessageSink
    {
        private readonly bool hasConsole = CheckHasConsole();
        private StringBuilder stringBuilder = new StringBuilder();
        private MicroLock stringBuilderLock = new MicroLock();

        public class Config : Configuration
        {
            public string format = ">>{0}: {1} | {2} | {3} | {4} | {9}<<\n";
            internal Loglevel logFileLoglevel = Loglevel.WARNING;
        }

        public Config config = new Config();

        public void Post<M>(in M message)
        {
            if (!hasConsole) return;

            config.TryUpdateFromStorage(true);

            if (message is LogMessage logMessage)
            {
                if (logMessage.level <= config.logFileLoglevel)
                {
                    string strMsg;
                    using (stringBuilderLock.Lock())
                    {
                        strMsg = logMessage.PrintToStringBuilder(stringBuilder).ToString();
                        stringBuilder.Clear();
                    }
                    Console.WriteLine(strMsg);
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

        public static bool CheckHasConsole()
        {
            try
            {
                var x = Console.WindowHeight;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }
    }
}