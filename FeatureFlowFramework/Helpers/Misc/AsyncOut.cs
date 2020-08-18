using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Misc
{
    public struct AsyncOut<T, OUT>
    {
        private readonly T returnValue;
        private readonly OUT result;

        public AsyncOut(T returnValue, OUT result)
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

        public static implicit operator AsyncOut<T, OUT>((T returnValue ,OUT result) tuple) => new AsyncOut<T, OUT>(tuple.returnValue, tuple.result);        
    }
}