using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.Aspects
{
    public static partial class AspectRegistry
    {
        private static bool active = true;
        private static ConditionalWeakTable<object, FinalizationTrigger> weakObjectRegister = new ConditionalWeakTable<object, FinalizationTrigger>();
        private static Dictionary<long, AspectData> handleToAspectData = new Dictionary<long, AspectData>();
        private static Sender notificationSender = new Sender();
        private static ActiveForwarder notificationForwarder = new ActiveForwarder();
        private static long handleCounter = 0;

        static AspectRegistry()
        {
            notificationSender.ConnectTo(notificationForwarder);
        }

        public static IDataFlowSource NotificationSource => notificationForwarder;
        public static bool IsActive => active;

        public static void Disable()
        {
            if (!active) return;

            active = false;
            weakObjectRegister = new ConditionalWeakTable<object, FinalizationTrigger>();
            handleToAspectData = new Dictionary<long, AspectData>();
            notificationSender.Send(new ActivationStatusNotification(false));
        }

        public static void Enable()
        {
            if (active) return;

            active = true;
            notificationSender.Send(new ActivationStatusNotification(true));
        }

        private static bool FindHandleFor(object obj, out long handle)
        {
            if (!active)
            {
                handle = default;
                return false;
            }

            if (weakObjectRegister.TryGetValue(obj, out FinalizationTrigger trigger))
            {
                handle = trigger.ObjectHandle;
                return true;
            }
            else
            {
                handle = default;
                return false;
            }
        }

        public static bool TryGetAspectData(long handle, out AspectData data)
        {
            if (!active)
            {
                data = default;
                return false;
            }

            lock (handleToAspectData)
            {
                return handleToAspectData.TryGetValue(handle, out data);
            }
        }

        public static object GetObject(long handle)
        {
            return TryGetAspectData(handle, out AspectData data) ? data.Obj : null;
        }

        public static AspectData GetAspectData<T>(this T obj) where T : class
        {
            lock (handleToAspectData)
            {
                var handle = obj.GetAspectHandle();
                return TryGetAspectData(handle, out AspectData data) ? data : null;
            }
        }

        public static long GetAspectHandle<T>(this T obj) where T : class => AddObject(obj);

        public static long AddObject<T>(T obj) where T : class
        {
            if (!active || obj == null) return default;

            lock (handleToAspectData)
            {
                if (FindHandleFor(obj, out long existingHandle)) return existingHandle; // If object is already registered just return the handle
                long handle = ++handleCounter;
                AspectData data = new AspectData(obj, handle);
                weakObjectRegister.Add(obj, new FinalizationTrigger(handle));
                if (!handleToAspectData.TryAdd(handle, data)) return AddObject(obj); // If handle is duplicate, try again!
                notificationSender.Send(new ObjectAddedNotification(handle, data));
                return handle;
            }
        }

        public static IEnumerable<AspectData> GetAllAspectData()
        {
            if (!active) return default;

            lock (handleToAspectData)
            {
                return handleToAspectData.Values;
            }
        }

        public static void RemoveObject(object obj)
        {
            if (!active) return;

            if (FindHandleFor(obj, out long handle)) CleanUpObjectData(handle);
        }

        public static void RemoveObject(long handle)
        {
            if (!active) return;

            CleanUpObjectData(handle);
        }

        private static void CleanUpObjectData(long handle)
        {
            if (!active) return;

            lock (handleToAspectData)
            {
                if (handleToAspectData.TryGetValue(handle, out AspectData data))
                {
                    var obj = data.Obj;
                    if (obj != null) weakObjectRegister.Remove(obj);
                    handleToAspectData.Remove(handle);
                    notificationSender.Send(new ObjectRemovedNotification(handle, data));
                }
            }
        }

        public static T GetAspectInterface<T>(this object obj, Predicate<T> condition = null) where T : class
        {
            if (obj is T objT) return objT;
            if (obj is AspectAddOn addon) addon.TryGetObject(out obj);
            return obj.GetAspectData().TryGetAspectInterface(out T aspectInterface, condition) ? aspectInterface : null;
        }

        public static T GetAspectInterface<T, ADDON>(this object obj, Predicate<T> condition = null) where T : class where ADDON : AspectAddOn, T, new()
        {
            if (obj is T objT) return objT;
            if (obj is AspectAddOn addon) addon.TryGetObject(out obj);
            return obj.GetAspectData().TryGetAspectInterface(out T aspectInterface, condition) ? aspectInterface : obj.AddAspectAddOn(new ADDON());
        }

        public static T AddAspectAddOn<T>(this object obj, T addOn) where T : AspectAddOn
        {
            obj.GetAspectData().AddAddOn(addOn);
            return addOn;
        }

        public readonly struct ObjectRemovedNotification
        {
            public readonly long objectHandle;
            public readonly AspectData aspectData;

            public ObjectRemovedNotification(long objectHandle, AspectData aspectData)
            {
                this.objectHandle = objectHandle;
                this.aspectData = aspectData;
            }
        }

        public readonly struct ObjectAddedNotification
        {
            public readonly long objectHandle;
            public readonly AspectData aspectData;

            public ObjectAddedNotification(long objectHandle, AspectData aspectData)
            {
                this.objectHandle = objectHandle;
                this.aspectData = aspectData;
            }
        }

        public readonly struct ActivationStatusNotification
        {
            public readonly bool isActive;

            public ActivationStatusNotification(bool isActive)
            {
                this.isActive = isActive;
            }
        }

        private class FinalizationTrigger
        {
            private readonly long objectHandle;

            public FinalizationTrigger(long objectHandle)
            {
                this.objectHandle = objectHandle;
            }

            ~FinalizationTrigger()
            {
                CleanUpObjectData(objectHandle);
            }

            public long ObjectHandle => objectHandle;
        }
    }
}