using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureFlowFramework.Aspects
{
    public class AspectData
    {
        private WeakReference<object> objectRef;
        private readonly long objectHandle;
        private List<AspectAddOn> aspectAddOns = new List<AspectAddOn>();
        FeatureLock aspectAddonsLock = new FeatureLock();

        public AspectData(object obj, long objectHandle)
        {
            this.objectRef = new WeakReference<object>(obj);
            this.objectHandle = objectHandle;
        }

        public object Obj => objectRef.GetTargetOrDefault();

        public long ObjectHandle => objectHandle;

        public void AddAddOn(AspectAddOn addOn)
        {
            addOn.SetObjectRef(this.objectRef);
            using(aspectAddonsLock.ForWriting())            
            {
                aspectAddOns.Add(addOn);
            }
        }

        public bool TryGetAspectInterface<T>(out T aspectInterface, Predicate<T> condition = null)
        {
            aspectInterface = default;
            if(!objectRef.TryGetTarget(out object obj)) return false;
            if(obj is T objT && (condition?.Invoke(objT) ?? true))
            {
                aspectInterface = objT;
                return true;
            }
            foreach(var addOn in aspectAddOns)
            {
                if(addOn is T addOnT && (condition?.Invoke(addOnT) ?? true))
                {
                    aspectInterface = addOnT;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetAspectInterfaces<T>(out T[] aspectInterfaces, Predicate<T> condition = null)
        {
            LazySlim<List<T>> interfaceList = new LazySlim<List<T>>();
            if(!objectRef.TryGetTarget(out object obj))
            {
                if(aspectAddOns.Count == 0)
                {
                    if(obj is T objT && (condition?.Invoke(objT) ?? true))
                    {
                        aspectInterfaces = objT.ToSingleEntryArray();
                        return true;
                    }
                }
                else
                {
                    if(obj is T objT && (condition?.Invoke(objT) ?? true)) interfaceList.Obj.Add(objT);
                    foreach(var addOn in aspectAddOns)
                    {
                        if(addOn is T addOnT && (condition?.Invoke(addOnT) ?? true)) interfaceList.Obj.Add(addOnT);
                    }

                    if(interfaceList.IsInstantiated)
                    {
                        aspectInterfaces = interfaceList.Obj.ToArray();
                        return true;
                    }
                }
            }

            aspectInterfaces = Array.Empty<T>();
            return false;
        }
    }
}