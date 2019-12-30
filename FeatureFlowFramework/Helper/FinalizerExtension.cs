using System;

namespace FeatureFlowFramework.Helper
{
    public static class FinalizerExtension
    {
        /// The final action will be called when the observed object is garbage collected.
        /// Warning: This is an advanced feature that should only be used if you know what you are doing.
        /// Never use it with an object that has an own finalizer (e.g. usually IDisposable types) and 
        /// never access any referenced object that has an own finalizer, because they might already be finalized.
        public static T OnFinalize<T>(this T observedObj, Action<T> finalAction) where T : class
        {
            new Finalizer<T>(observedObj, finalAction);
            return observedObj;
        }

        /// The final action will be called when the observed object is garbage collected.
        /// Warning: This is an advanced feature that should only be used if you know what you are doing.
        /// Never use it with an object that has an own finalizer (e.g. usually IDisposable types) and 
        /// never access any referenced object that has an own finalizer, because they might already be finalized.
        private class Finalizer<T> where T : class
        {
            readonly T observedObj;
            readonly Action<T> finalAction;
            public Finalizer(T observedObj, Action<T> finalAction)
            {
                this.finalAction = finalAction;
                this.observedObj = observedObj;
                this.KeepAlive(observedObj);
            }

            ~Finalizer()
            {
                finalAction(this.observedObj);
            }
        }

    }
}
