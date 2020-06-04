using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.Services.MetaData
{
    public class MetaData
    {
        static ConditionalWeakTable<object, MetaData> objects = new ConditionalWeakTable<object, MetaData>();
        static Dictionary<long, MetaData> handles = new Dictionary<long, MetaData>();
        static FeatureLock handlesLock = new FeatureLock();
        static long handleIdCounter = 0;
        static Sender<ObjectHandleInfo> updateSender = new Sender<ObjectHandleInfo>();


        readonly ObjectHandle handle;
        LazySlim<Dictionary<string, object>> data;
        readonly WeakReference<object> objRef;
        FeatureLock objLock = new FeatureLock();
        Sender<MetaDataUpdateInfo> metaDataUpdateSender;

        public Sender<MetaDataUpdateInfo> MetaDataUpdateSender
        {
            get
            {
                if(metaDataUpdateSender == null) metaDataUpdateSender = new Sender<MetaDataUpdateInfo>();
                return metaDataUpdateSender;
            }
        }

        public MetaData(object obj)
        {
            objRef = new WeakReference<object>(obj);
            handle = new ObjectHandle(++handleIdCounter);
        }

        public static Sender<ObjectHandleInfo> UpdateSender
        {
            get
            {
                if (updateSender == null) updateSender = new Sender<ObjectHandleInfo>();
                return updateSender;
            }
        }

        ~MetaData()
        {
            using(handlesLock.ForWriting())
            {
                updateSender?.Send(new ObjectHandleInfo(handle, true));

                handles.Remove(handle.id);
            }
        }

        public static bool TryGetObjectType(ObjectHandle handle, out Type type)
        {
            using (handlesLock.ForReading())
            {
                type = default;
                return handles.TryGetValue(handle.id, out MetaData metaData) && metaData.TryGetObjectType(out type);
            }
        }

        public static bool Exists(ObjectHandle handle)
        {
            using (handlesLock.ForReading())
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

        protected bool TryGetObjectType(out Type type)
        {
            type = default;
            if(!objRef.TryGetTarget(out object target)) return false;
            type = target.GetType();
            return true;
        }

        protected static MetaData GetOrCreate(object obj)
        {
            if(objects.TryGetValue(obj, out MetaData metaData)) return metaData;
            else
            {
                metaData = new MetaData(obj);
                objects.Add(obj, metaData);
                using(handlesLock.ForWriting()) handles[metaData.handle.id] = metaData;

                updateSender?.Send(new ObjectHandleInfo(metaData.handle, false));

                return metaData;
            }
        }

        public static ObjectHandle GetHandle(object obj)
        {
            return GetOrCreate(obj).handle;
        }

        public static bool TryGetObject<T>(ObjectHandle handle, out T obj) where T:class
        {
            using(handlesLock.ForReading())
            {
                if(handles.TryGetValue(handle.id, out MetaData metaData) &&
                   metaData.objRef.TryGetTarget(out object untyped) &&
                   untyped is T typed)
                {
                    obj = typed;
                    return true;
                }
                else
                {
                    obj = null;
                    return false;
                }
            }
        }

        public static void SetMetaData<D>(object obj, string key, D data)
        {
            var metaData = GetOrCreate(obj);
            using(metaData.objLock.ForWriting()) metaData.data.Obj[key] = data;

            metaData.metaDataUpdateSender?.Send(new MetaDataUpdateInfo(metaData.handle, key));
        }

        public static bool TryGetMetaData<D>(object obj, string key, out D data)
        {
            data = default;
            if(!objects.TryGetValue(obj, out MetaData metaData)) return false;
            using(metaData.objLock.ForReading())
            {
                if(metaData.data.IsInstantiated &&
                    metaData.data.Obj.TryGetValue(key, out object untypedData) &&
                    untypedData is D typedData)
                {
                    data = typedData;
                    return true;
                }
                else return false;
            }
        }

        public static FeatureLock GetLock(object obj)
        {
            var metaData = GetOrCreate(obj);
            return metaData.objLock;
        }

    }

}