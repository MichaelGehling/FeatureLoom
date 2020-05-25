namespace FeatureFlowFramework.Helpers
{
    public struct AsyncOutResult<T, OUT>
    {
        private readonly T returnValue;
        private readonly OUT result;

        public AsyncOutResult(T returnValue, OUT result)
        {
            this.returnValue = returnValue;
            this.result = result;
        }

        public T Out(out OUT result)
        {
            result = this.result;
            return returnValue;
        }

        public T ReturnValue => returnValue;

        public static implicit operator AsyncOutResult<T, OUT>((T returnValue ,OUT result) tuple) => new AsyncOutResult<T, OUT>(tuple.returnValue, tuple.result);
    }
}