using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Helper
{
    public static class Helpers
    {
        public static bool Try<T>(Func<T> function, out T result)
        {
            try
            {
                result = function();
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public static bool Try(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
