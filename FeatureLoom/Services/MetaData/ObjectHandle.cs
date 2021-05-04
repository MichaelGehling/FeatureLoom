namespace FeatureLoom.Services.MetaData
{
    public readonly struct ObjectHandle
    {
        public readonly long id;

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
        public bool ObjExists() => MetaData.Exists(this);
    }
}