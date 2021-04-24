using System.Threading;

namespace FeatureFlowFramework.Helpers.Misc
{
    public struct LazyValue<T> where T : class, new()
    {
        private T obj;

        public T Obj
        {
            get => obj ?? Create();
            set => obj = value;
        }

        public T ObjIfExists => obj;

        public bool Exists => obj != null;

        private T Create()
        {
            Interlocked.CompareExchange(ref obj, new T(), null);
            return obj;
        }

        public static implicit operator T(LazyValue<T> lazy) => lazy.Obj;
    }
}