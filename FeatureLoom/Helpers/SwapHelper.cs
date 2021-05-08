namespace FeatureLoom.Helpers
{
    public static class SwapHelper
    {
        public static void Swap<T>(ref T obj1, ref T obj2)
        {
            T temp = obj1;
            obj1 = obj2;
            obj2 = temp;
        }
    }
}
