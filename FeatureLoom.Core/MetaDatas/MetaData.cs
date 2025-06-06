﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.MetaDatas
{
    public class MetaData
    {
        private static ConditionalWeakTable<object, MetaData> objects = new ConditionalWeakTable<object, MetaData>();
        private static Dictionary<long, WeakReference<MetaData>> handles = new Dictionary<long, WeakReference<MetaData>>();
        private static FeatureLock handlesLock = new FeatureLock();
        private static long handleIdCounter = 0;
        private static LazyValue<Sender<ObjectHandleInfo>> globalUpdateSender;

        private readonly ObjectHandle handle;
        private LazyValue<Dictionary<string, object>> data;
        private readonly WeakReference<object> objRef;
        private MicroLock objLock = new MicroLock();
        private LazyValue<Sender> metaDataUpdateSender;

        public static Sender<ObjectHandleInfo> UpdateSender => globalUpdateSender.Obj;
        public Sender MetaDataUpdateSender => metaDataUpdateSender.Obj;

        protected MetaData(object obj)
        {
            objRef = new WeakReference<object>(obj);
            handle = new ObjectHandle(Interlocked.Increment(ref handleIdCounter));
        }        

        ~MetaData()
        {
            using (handlesLock.Lock())
            {
                globalUpdateSender.ObjIfExists?.Send(new ObjectHandleInfo(handle, true));
                metaDataUpdateSender.ObjIfExists?.Send(new DestructionInfo());

                handles.Remove(handle.id);
            }
        }

        public static bool TryGetObjectType(ObjectHandle handle, out Type type)
        {
            using (handlesLock.LockReadOnly())
            {
                type = default;
                return handles.TryGetValue(handle.id, out WeakReference<MetaData> metaDataRef) && metaDataRef.TryGetTarget(out MetaData metaData) && metaData.TryGetObjectType(out type);
            }
        }

        public static bool Exists(ObjectHandle handle)
        {
            using (handlesLock.LockReadOnly())
            {
                return handles.ContainsKey(handle.id);
            }
        }

        public readonly struct ObjectHandleInfo
        {
            public readonly ObjectHandle handle;
            public readonly bool removing;

            public ObjectHandleInfo(ObjectHandle handle, bool removing)
            {
                this.handle = handle;
                this.removing = removing;
            }
        }

        public static void Register(object obj)
        {
            GetHandle(obj);
        }

        public static void Unregister(object obj)
        {
            objects.Remove(obj);
        }

        public readonly struct MetaDataUpdateInfo
        {
            public readonly ObjectHandle handle;
            public readonly string updatedKey;

            public MetaDataUpdateInfo(ObjectHandle handle, string updatedKey)
            {
                this.handle = handle;
                this.updatedKey = updatedKey;
            }
        }

        public readonly struct DestructionInfo
        {

        }

        protected bool TryGetObjectType(out Type type)
        {
            type = default;
            if (!objRef.TryGetTarget(out object target)) return false;
            type = target.GetType();
            return true;
        }

        public static MetaData GetOrCreate(object obj)
        {
            if (objects.TryGetValue(obj, out MetaData metaData)) return metaData;
            else
            {
                metaData = new MetaData(obj);
                objects.Add(obj, metaData);
                using (handlesLock.Lock()) handles[metaData.handle.id] = new WeakReference<MetaData>(metaData);

                globalUpdateSender.ObjIfExists?.Send(new ObjectHandleInfo(metaData.handle, false));

                return metaData;
            }
        }

        public static ObjectHandle GetHandle(object obj)
        {
            return GetOrCreate(obj).handle;
        }

        public static bool TryGetObject<T>(ObjectHandle handle, out T obj) where T : class
        {
            using (handlesLock.LockReadOnly())
            {
                obj = null;
                if (!handles.TryGetValue(handle.id, out WeakReference<MetaData> metaDataRef)) return false;
                if (!metaDataRef.TryGetTarget(out MetaData metaData)) return false;
                if (!metaData.objRef.TryGetTarget(out object untyped)) return false;
                if (!(untyped is T typed)) return false;

                obj = typed;
                return true;
            }
        }

        public static void SetMetaData<D>(object obj, string key, D data)
        {
            var metaData = GetOrCreate(obj);
            using (metaData.objLock.Lock()) metaData.data.Obj[key] = data;

            metaData.metaDataUpdateSender.ObjIfExists?.Send(new MetaDataUpdateInfo(metaData.handle, key));
        }

        public static bool TryGetMetaData<D>(object obj, string key, out D data)
        {
            data = default;
            if (!objects.TryGetValue(obj, out MetaData metaData)) return false;
            using (metaData.objLock.LockReadOnly())
            {
                if (!metaData.data.Exists) return false;
                if (!metaData.data.Obj.TryGetValue(key, out object untypedData)) return false;
                if (!(untypedData is D typedData)) return false;
                
                data = typedData;
                return true;
            }
        }
    }
}