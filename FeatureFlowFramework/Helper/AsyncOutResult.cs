namespace FeatureFlowFramework.Helper
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
    }

    public struct AsyncOutResult<T, OUT1, OUT2>
    {
        private readonly T returnValue;
        private readonly OUT1 result1;
        private readonly OUT2 result2;

        public AsyncOutResult(T returnValue, OUT1 result1, OUT2 result2)
        {
            this.returnValue = returnValue;
            this.result1 = result1;
            this.result2 = result2;
        }

        public T Out(out OUT1 result1, out OUT2 result2)
        {
            result1 = this.result1;
            result2 = this.result2;
            return returnValue;
        }
    }
}
