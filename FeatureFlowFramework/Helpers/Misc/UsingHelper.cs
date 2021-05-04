using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers.Misc
{
    public class UsingHelper : IDisposable
    {
        readonly Action after;

        public UsingHelper(Action before, Action after)
        {
            before?.Invoke();
            this.after = after;
        }

        public void Dispose()
        {
            after();
        }        
    }
}
