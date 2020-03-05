using FeatureFlowFramework.Helper;

namespace FeatureFlowFramework.Helper
{
    public struct WaitHandleCollection
    {
        IAsyncWaitHandle[] array;

        public IAsyncWaitHandle[] Init(params IAsyncWaitHandle[] newArray)
        {
            array = newArray;
            return All;
        }

        public IAsyncWaitHandle[] All => array;        

    }

}
