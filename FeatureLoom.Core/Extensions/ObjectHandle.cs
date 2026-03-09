using System;

namespace FeatureLoom.Extensions
{
    public readonly struct ObjectHandle : IEquatable<ObjectHandle>
    {
        public readonly long id;

        public static ObjectHandle Invalid => new ObjectHandle(0);

        public bool IsInvalid => id == 0;
        public bool IsValid => id != 0;

        public ObjectHandle(long id)
        {
            this.id = id;
        }

        public override string ToString()
        {
            return id.ToString();
        }

        public bool TryGetObject<T>(out T obj) where T : class => MetaData.TryGetObject(this, out obj);

        public T GetObject<T>() where T : class
        {
            if (MetaData.TryGetObject(this, out T obj)) return obj;
            else return null;
        }

        public bool TrySetMetaData<D>(string key, D data)
        {            
            if (!TryGetObject(out object obj)) return false;
            obj.SetMetaData(key, data);
            return true;
        }

        public bool TryGetMetaData<D>(string key, out D data)
        {
            data = default;
            if (!TryGetObject(out object obj)) return false;
            return obj.TryGetMetaData(key, out data);
        }

        public bool ObjExists() => MetaData.Exists(this);

        public bool Equals(ObjectHandle other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ObjectHandle otherHandle)) return false;
            return this.Equals(otherHandle);
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();            
        }

        public static bool operator ==(ObjectHandle handle1, ObjectHandle handle2)
        {
            return handle1.Equals(handle2);
        }

        public static bool operator !=(ObjectHandle handle1, ObjectHandle handle2)
        {
            return !handle1.Equals(handle2);
        }

        public static implicit operator ObjectHandle(long id) => new ObjectHandle(id);
    }
}