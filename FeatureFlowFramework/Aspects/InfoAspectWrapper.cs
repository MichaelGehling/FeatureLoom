using FeatureFlowFramework.Helper;
using System.Collections.Generic;
using System.Linq;

namespace FeatureFlowFramework.Aspects
{
    public struct InfoAspectWrapper<T> where T : class
    {
        public T obj;
        private readonly long handle;
        private IAspectValueContainer aspectInfoValues;
        private const string collectionName = "Info";

        public InfoAspectWrapper(T obj)
        {
            this.obj = obj;
            var data = obj.GetAspectData();
            handle = data.ObjectHandle;
            if (!data.TryGetAspectInterface(out aspectInfoValues, IsInfoCollection))
            {
                aspectInfoValues = new AspectValueAddOn(collectionName);
                data.AddAddOn(aspectInfoValues as AspectValueAddOn);
            }
        }

        private static bool IsInfoCollection(IAspectValueContainer aspectValue) => aspectValue.CollectionName == collectionName;

        public T LeaveInfoAspect => obj;

        public static implicit operator InfoAspectWrapper<T>(T obj) => new InfoAspectWrapper<T>(obj);

        public static implicit operator T(InfoAspectWrapper<T> wrapper) => wrapper.obj;

        public InfoAspectWrapper<T> SetName(string name)
        {
            aspectInfoValues.Set("name", name);
            return this;
        }

        public string Name
        {
            get => aspectInfoValues.TryGet("name", out string name) ? name : null;
            set => SetName(value);
        }

        public InfoAspectWrapper<T> SetDescription(string description)
        {
            aspectInfoValues.Set("description", description);
            return this;
        }

        public string Description
        {
            get => aspectInfoValues.TryGet("description", out string description) ? description : null;
            set => SetDescription(value);
        }

        public InfoAspectWrapper<T> SetParent<P>(P parent) where P : class
        {
            AddParentAndChild(new InfoAspectWrapper<P>(parent), this);
            return this;
        }

        public object Parent
        {
            get => aspectInfoValues.TryGet("parent", out object parent) ? parent : null;
            set => SetParent(value);
        }

        public InfoAspectWrapper<T> AddChild<C>(C child) where C : class
        {
            AddParentAndChild(this, new InfoAspectWrapper<C>(child));
            return this;
        }

        public IEnumerable<object> Children
        {
            get => aspectInfoValues.TryGet("children", out HashSet<long> children).IfTrue(children, null)?
                .Select(handle => AspectRegistry.TryGetAspectData(handle, out AspectData data).IfTrue(data.Obj, null))
                .Where(obj => obj != null);
        }

        private void AddParentAndChild<P, C>(InfoAspectWrapper<P> parent, InfoAspectWrapper<C> child) where P : class where C : class
        {
            if (!parent.aspectInfoValues.TryGet("children", out HashSet<long> childrenSet))
            {
                childrenSet = new HashSet<long>();
                parent.aspectInfoValues.Set("children", childrenSet);
            }
            if (childrenSet.Add(child.handle))
            {
                child.aspectInfoValues.Set("parent", parent.handle);
            }
        }
    }

    public static class AspectAccessExtension
    {
        public static InfoAspectWrapper<T> EnterInfoAspect<T>(this T obj) where T : class
        {
            return new InfoAspectWrapper<T>(obj);
        }
    }
}