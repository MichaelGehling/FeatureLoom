using System.Threading;

namespace FeatureLoom.Helpers
{
    public struct LazyValue<T> where T : class, new()
    {
        private T obj;

        public LazyValue(T obj)
        {
            this.obj = obj;
        }        

        public T Obj
        {
            get => obj ?? Create();
            set => obj = value;
        }

        public T ObjIfExists => obj;

        public bool Exists => obj != null;

        public void RemoveObj()
        {
            obj = default;
        }

        private T Create()
        {
            Interlocked.CompareExchange(ref obj, new T(), null);
            return obj;
        }        

        public static implicit operator T(LazyValue<T> lazy) => lazy.Obj;
    }
}