using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers
{
    public static class ConsoleHelper
    {
        static FeatureLock consoleLock = new FeatureLock();

        public static void UseLocked(Action consoleAction)
        {
            using (consoleLock.Lock())
            {
                consoleAction();
            }
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
    }
}
