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

        public static UsingHelper Do(Action before, Action after) => new UsingHelper(before, after);
        public static UsingHelper Do(Action after) => new UsingHelper(null , after);
    }
}