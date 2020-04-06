using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FeatureFlowFramework.Aspects.AppStructure
{
    public class AppStructureAddOn : AspectAddOn, IAcceptsName, IAcceptsDescription, IAcceptsParent, IAcceptsChildren
    {
        private string name = "";
        private string description = "";
        private long parentHandle = 0;
        private HashSet<long> childHandles = null;
        FeatureLock childHandlesLock = new FeatureLock();

        public string Name { get => name; set => name = value; }
        public string Description { get => description; set => description = value; }
        public object Parent { get => AspectRegistry.GetObject(parentHandle); set => parentHandle = value.GetAspectHandle(); }

        //TODO: invalid handles should be removed from this.childHandles.
        public IEnumerable<object> Children => childHandles?.Select(handle => AspectRegistry.GetObject(handle)).Where(obj => obj != null) ?? Array.Empty<object>();

        public IAcceptsChildren AddChild(object child, string childName = null)
        {
            if(child is IUpdateAppStructureAspect childUpdate) childUpdate.TryUpdateAppStructureAspects(Timeout.InfiniteTimeSpan);
            if(childHandles == null) childHandles = new HashSet<long>();
            using (childHandlesLock.ForWriting()) childHandles.Add(child.GetAspectHandle());
            if(childName != null) child.GetAspectInterface<IAcceptsName, AppStructureAddOn>().SetName(childName);
            return this;
        }

        public IAcceptsChildren RemoveChild(object child)
        {
            if(childHandles == null) return this;
            using (childHandlesLock.ForWriting()) childHandles.Remove(child.GetAspectHandle());
            return this;
        }

        public IAcceptsChildren RemoveAllChildren()
        {
            using (childHandlesLock.ForWriting()) childHandles?.Clear();
            return this;
        }

        protected override void OnSetObject(object obj)
        {
            UpdateFromObject();
        }

        public void UpdateFromObject()
        {
            if(!TryGetObject(out object obj)) return;
            if(obj is IHasName nameObj) name = nameObj.Name;
            if(obj is IHasDescription descriptionObj) description = descriptionObj.Description;
            if(obj is IHasParent parentObj) Parent = parentObj.Parent;
            if(obj is IHasChildren childrenObj)
            {
                RemoveAllChildren();
                var children = childrenObj.Children;
                foreach(var child in children)
                {
                    AddChild(child);
                }
            }
        }

        public IAcceptsName SetName(string name)
        {
            Name = name;
            return this;
        }

        public IAcceptsDescription SetDescription(string description)
        {
            Description = description;
            return this;
        }

        public IAcceptsParent SetParent(object parent)
        {
            Parent = parent;
            return this;
        }
    }
}