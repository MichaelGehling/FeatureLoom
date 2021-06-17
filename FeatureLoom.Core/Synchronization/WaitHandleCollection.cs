namespace FeatureLoom.Synchronization
{
    public struct WaitHandleCollection
    {
        private IAsyncWaitHandle[] array;

        public IAsyncWaitHandle[] Init(params IAsyncWaitHandle[] newArray)
        {
            array = newArray;
            return All;
        }

        public IAsyncWaitHandle[] All => array;
    }
}