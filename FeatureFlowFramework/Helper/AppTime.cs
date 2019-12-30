using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public static class AppTime
    {
        private static Stopwatch stopWatch = new Stopwatch();

        static AppTime()
        {
            stopWatch.Start();
        }

        public static Func<DateTime> timeFactory = () => DateTime.UtcNow;
        public static DateTime Now => timeFactory();
        public static TimeSpan Elapsed => stopWatch.Elapsed;
        public static TimeKeeper TimeKeeper => new TimeKeeper(Elapsed);
    }

}
