namespace FeatureLoom.Helpers
{
    /// <summary>
    /// Provides a generic method to swap the values of two variables.
    /// </summary>
    public static class SwapHelper
    {
        /// <summary>
        /// Swaps the values of two variables of the same type.
        /// </summary>
        /// <typeparam name="T">The type of the variables to swap.</typeparam>
        /// <param name="obj1">The first variable, passed by reference.</param>
        /// <param name="obj2">The second variable, passed by reference.</param>
        /// <remarks>
        /// This method exchanges the values of <paramref name="obj1"/> and <paramref name="obj2"/>.
        /// Both parameters must be passed by reference.
        /// </remarks>
        public static void Swap<T>(ref T obj1, ref T obj2)
        {
            T temp = obj1;
            obj1 = obj2;
            obj2 = temp;
        }
    }
}
