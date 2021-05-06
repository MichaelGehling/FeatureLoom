using System;

namespace FeatureLoom.Helpers
{
    public readonly struct UsingHelper : IDisposable
    {
        private readonly Action after;

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