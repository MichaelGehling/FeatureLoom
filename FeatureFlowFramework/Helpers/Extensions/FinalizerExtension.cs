using FeatureLoom.Services;
using FeatureLoom.Services.MetaData;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Helpers.Extensions
{
    public static class FinalizerExtension
    {
        /// The final action will be called when the object is garbage collected.
        /// Note: Because this Finalizer uses the MetaData service, the action will also be called for the case the object 
        /// is unregistered from the MetaData service.
        /// Warning: This is an advanced feature that should only be used if you know what you are doing.
        /// Never use it with an object that has an own finalizer (e.g. usually IDisposable types) and
        /// never access any referenced object that has an own finalizer, because they might already be finalized.
        public static T OnFinalize<T>(this T obj, Action finalAction) where T : class
        {
            var finalizer = new Finalizer(finalAction);
            obj.SetMetaData("Finalizer", finalizer);
            return obj;
        }
        
        private class Finalizer
        {
            private readonly Action finalAction;

            public Finalizer(Action finalAction)
            {
                this.finalAction = finalAction;
            }

            ~Finalizer()
            {
                finalAction();
            }
        }
    }
}