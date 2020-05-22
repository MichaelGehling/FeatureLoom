using System;

namespace FeatureFlowFramework.Aspects
{
    public abstract class AspectAddOn
    {
        private WeakReference<object> objectRef = null;

        internal void SetObjectRef(WeakReference<object> objectRef)
        {
            if(this.objectRef == null) this.objectRef = objectRef;
            else throw new Exception("The objectRef of an AspectExtension is not allowed to be changed!");
        }

        protected virtual void OnSetObject(object obj)
        {
        }

        public bool TryGetObject<T>(out T obj) where T : class
        {
            objectRef.TryGetTarget(out object untypedObj);
            obj = untypedObj as T;
            return obj != null;
        }
    }
}