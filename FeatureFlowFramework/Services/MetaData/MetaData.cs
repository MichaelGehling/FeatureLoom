using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.Services.MetaData
{
    public abstract class MetaData
    {
        protected static ConditionalWeakTable<object, MetaData> objects = new ConditionalWeakTable<object, MetaData>();
        protected static Dictionary<long, MetaData> handles = new Dictionary<long, MetaData>();
        protected static FeatureLock handlesLock = new FeatureLock();
        protected static long handleIdCounter = 0;
        protected static Sender<ObjectHandleInfo> updateSender = new Sender<ObjectHandleInfo>();

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
            FinalizeIt();                        
        }

        protected abstract void FinalizeIt();

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

        protected abstract bool TryGetObjectType(out Type type);

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

        public static void Register<T>(T obj) where T : class
        {
            MetaData<T>.GetHandle(obj);
        }

        public static void Unregister<T>(T obj) where T : class
        {
            objects.Remove(obj);
        }
    }

    public class MetaData<T> : MetaData where T : class
    {
        readonly ObjectHandle handle;
        LazySlim<Dictionary<string, object>> data;
        readonly WeakReference<T> objRef;
        FeatureLock objLock = new FeatureLock();
        Sender<MetaDataUpdateInfo> metaDataUpdateSender;

        public Sender<MetaDataUpdateInfo> MetaDataUpdateSender
        {
            get
            {
                if (metaDataUpdateSender == null) metaDataUpdateSender = new Sender<MetaDataUpdateInfo>();
                return metaDataUpdateSender;
            }
        }

        public MetaData(T obj)
        {
            objRef = new WeakReference<T>(obj);
            handle = new ObjectHandle(++handleIdCounter);
        }

        protected override void FinalizeIt()
        {
            using (handlesLock.ForWriting())
            {
                updateSender?.Send(new ObjectHandleInfo(handle, true));

                handles.Remove(handle.id);
            }
        }

        protected override bool TryGetObjectType(out Type type)
        {
            type = default;
            if (!objRef.TryGetTarget(out T target)) return false;
            type = target.GetType();
            return true;
        }

        #region static

        protected static MetaData<T> GetOrCreate(T obj)
        {
            if (objects.TryGetValue(obj, out MetaData untyped) && untyped is MetaData<T> metaData) return metaData;            
            else
            {
                metaData = new MetaData<T>(obj);
                objects.Add(obj, metaData);
                using (handlesLock.ForWriting()) handles[metaData.handle.id] = metaData;

                updateSender?.Send(new ObjectHandleInfo(metaData.handle, false));

                return metaData;
            }
        }

        public static ObjectHandle GetHandle(T obj)
        {
            return GetOrCreate(obj).handle;
        }

        public static bool TryGetObject(ObjectHandle handle, out T obj)
        {
            using (handlesLock.ForReading())
            {
                obj = default;
                return handles.TryGetValue(handle.id, out MetaData untyped) &&
                       untyped is MetaData<T> metaData &&
                       metaData.objRef.TryGetTarget(out obj);
            }
        }

        public static void SetMetaData<D>(T obj, string key, D data)
        {
            var metaData = GetOrCreate(obj);
            using (metaData.objLock.ForWriting()) metaData.data.Obj[key] = data;

            metaData.metaDataUpdateSender?.Send(new MetaDataUpdateInfo(metaData.handle, key));
        }

        public static bool TryGetMetaData<D>(T obj, string key, out D data)
        {
            data = default;
            if (!objects.TryGetValue(obj, out MetaData untyped) || !(untyped is MetaData<T> metaData)) return false;
            using (metaData.objLock.ForReading())
            {                
                if (metaData.data.IsInstantiated &&
                    metaData.data.Obj.TryGetValue(key, out object untypedData) && 
                    untypedData is D typedData)
                {
                    data = typedData;
                    return true;
                }
                else return false;
            }
        }

        public static FeatureLock GetLock(T obj)
        {
            var metaData = GetOrCreate(obj);
            return metaData.objLock;
        }

        #endregion

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
    }
}